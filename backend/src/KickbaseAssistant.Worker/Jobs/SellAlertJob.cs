using System.Globalization;
using System.Text;
using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Kickbase;
using KickbaseAssistant.Infrastructure.Kickbase.Dtos;
using KickbaseAssistant.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace KickbaseAssistant.Worker.Jobs;

/// <summary>
/// Phase 5 (Verkaufs-Alarm): the "Momentum" and "News" signals from the plan (the third signal,
/// "Startelf", is raised directly from LigaInsiderScrapeJob - see its comment). Runs once daily,
/// shortly after MarketValueSnapshotJob so today's snapshot is already in the DB.
///
/// Momentum: market value fell on each of the last 2 recorded days, or dropped more than 3% over
/// the last 3 recorded days. Needs at least 2-3 daily snapshots per player to say anything, so
/// this naturally stays quiet until the tracker has collected enough history.
///
/// News: GET /v4/base/news/{competitionId} is a general competition-wide editorial feed, not a
/// clean per-player injury feed - only items whose "url" links to a specific player are
/// considered, filtered by a plain keyword list (no ML - per the plan, "dafür brauchst du keine
/// KI"). Deliberately excludes items with an all-clear keyword like "Entwarnung" to cut obvious
/// false positives (a resolved injury scare showing up as a fresh sell signal).
/// </summary>
public class SellAlertJob(
    KickbaseApiClient kickbaseApiClient,
    NtfyPushService ntfyPushService,
    AppDbContext dbContext,
    ILogger<SellAlertJob> logger) : IJob
{
    private static readonly string[] NegativeNewsKeywords =
        ["verletzt", "verletzung", "ausfall", "fällt aus", "sperre", "gesperrt", "fraglich", "angeschlagen", "kreuzband", "bänderriss", "muskelfaserriss"];

    private static readonly string[] PositiveOverrideKeywords =
        ["entwarnung", "zurück im training", "einsatzbereit", "kein grund zur sorge", "wieder fit"];

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var squadPlayers = await dbContext.Players.Where(p => p.IsInSquad).ToListAsync(ct);
        var newAlerts = new List<(Player Player, string Reason)>();

        foreach (var player in squadPlayers)
        {
            var reason = await CheckMomentumAsync(player, ct);
            if (reason is not null
                && await SellAlertHelper.TryCreateAsync(dbContext, player.Id, SellAlertSignal.MarketValueDrop, reason, ct))
            {
                newAlerts.Add((player, reason));
            }
        }

        var competitionIds = squadPlayers.Select(p => p.CompetitionId.ToString()).Distinct().ToList();
        foreach (var competitionId in competitionIds)
        {
            IReadOnlyList<KickbaseNewsItemDto> news;
            try
            {
                news = await kickbaseApiClient.GetNewsAsync(competitionId, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "News-Feed für Competition {CompetitionId} konnte nicht geladen werden.", competitionId);
                continue;
            }

            foreach (var item in news)
            {
                if (item.PublishedAt < DateTimeOffset.UtcNow.AddDays(-5))
                {
                    continue;
                }

                var player = item.PlayerId is null
                    ? null
                    : squadPlayers.FirstOrDefault(p => p.Id == item.PlayerId);
                if (player is null)
                {
                    continue;
                }

                var text = $"{item.Title} {item.Subtitle} {item.Description}".ToLowerInvariant();
                var hasNegative = NegativeNewsKeywords.Any(text.Contains);
                var hasPositiveOverride = PositiveOverrideKeywords.Any(text.Contains);
                if (!hasNegative || hasPositiveOverride)
                {
                    continue;
                }

                var reason = $"News: \"{item.Title}\"";
                if (await SellAlertHelper.TryCreateAsync(dbContext, player.Id, SellAlertSignal.InjuryNews, reason, ct))
                {
                    newAlerts.Add((player, reason));
                }
            }
        }

        await dbContext.SaveChangesAsync(ct);

        if (newAlerts.Count > 0)
        {
            await PushAlertsAsync(newAlerts, ct);
        }

        logger.LogInformation(
            "Verkaufs-Alarm-Check fertig: {PlayerCount} Spieler geprüft, {AlertCount} neue Alarme.",
            squadPlayers.Count, newAlerts.Count);
    }

    private async Task<string?> CheckMomentumAsync(Player player, CancellationToken ct)
    {
        var lastThree = await dbContext.MarketValueSnapshots
            .Where(s => s.PlayerId == player.Id)
            .OrderByDescending(s => s.Date)
            .Take(3)
            .ToListAsync(ct);

        if (lastThree.Count >= 2 && lastThree[0].MarketValueChange < 0 && lastThree[1].MarketValueChange < 0)
        {
            return $"Marktwert 2 Tage in Folge gefallen ({FormatEuro(lastThree[0].MarketValueChange)}, {FormatEuro(lastThree[1].MarketValueChange)})";
        }

        if (lastThree.Count >= 3 && lastThree[2].MarketValue > 0)
        {
            var trend = (lastThree[0].MarketValue - lastThree[2].MarketValue) / (double)lastThree[2].MarketValue;
            if (trend <= -0.03)
            {
                return $"Marktwert {trend:P1} in 3 Tagen";
            }
        }

        return null;
    }

    private async Task PushAlertsAsync(List<(Player Player, string Reason)> alerts, CancellationToken ct)
    {
        var body = new StringBuilder();
        foreach (var (player, reason) in alerts)
        {
            body.AppendLine($"{player.Name}: {reason}");
        }

        await ntfyPushService.SendAsync(
            title: "Verkaufs-Alarm 💰",
            message: body.ToString().TrimEnd(),
            tags: "money_with_wings,warning",
            ct: ct);
    }

    private static string FormatEuro(int value) =>
        value.ToString("+#,0 €;-#,0 €;0 €", CultureInfo.GetCultureInfo("de-DE"));
}
