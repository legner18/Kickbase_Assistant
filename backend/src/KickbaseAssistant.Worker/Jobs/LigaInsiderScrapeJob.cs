using System.Text;
using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Kickbase;
using KickbaseAssistant.Infrastructure.LigaInsider;
using KickbaseAssistant.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace KickbaseAssistant.Worker.Jobs;

/// <summary>
/// Phase 4 (Aufstellungs-Wächter): scrapes LigaInsider's predicted lineup for every team that
/// has a player in the user's squad, and pushes a single combined ntfy notification whenever a
/// player's predicted status changes to something other than Starting. Always writes a
/// LineupPrediction row per player per run (append-only history), regardless of whether a push
/// was sent - the push itself is deduped by only firing on an actual status transition, so
/// running this a few times a day doesn't repeat the same alert.
///
/// Also feeds Phase 5's Verkaufs-Alarm: a player who *was* predicted Starting and now isn't is
/// exactly the "Startelf"-signal from the plan, so it's raised here (via SellAlertHelper, same
/// per-player-per-signal dedup as SellAlertJob) rather than re-deriving it from LineupPrediction
/// history a second time.
/// </summary>
public class LigaInsiderScrapeJob(
    KickbaseApiClient kickbaseApiClient,
    LigaInsiderClient ligaInsiderClient,
    NtfyPushService ntfyPushService,
    AppDbContext dbContext,
    ILogger<LigaInsiderScrapeJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var now = DateTimeOffset.UtcNow;

        var squadPlayers = await dbContext.Players.Where(p => p.IsInSquad).ToListAsync(ct);
        var playersByTeam = squadPlayers.GroupBy(p => p.TeamId).ToList();

        var competitionIds = squadPlayers.Select(p => p.CompetitionId.ToString()).Distinct().ToList();
        var teamNamesByCompetition = new Dictionary<string, Dictionary<string, string>>();
        foreach (var competitionId in competitionIds)
        {
            try
            {
                var table = await kickbaseApiClient.GetCompetitionTableAsync(competitionId, ct);
                teamNamesByCompetition[competitionId] = table.ToDictionary(t => t.Id, t => t.Name);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "LigaInsider: Liga-Tabelle für Competition {CompetitionId} konnte nicht geladen werden.", competitionId);
            }
        }

        var changedPlayers = new List<(Player Player, LineupStatus NewStatus, string? RawLabel)>();
        var newSellAlerts = new List<(Player Player, string Reason)>();

        foreach (var group in playersByTeam)
        {
            var teamId = group.Key;
            var competitionId = group.First().CompetitionId.ToString();
            if (!teamNamesByCompetition.TryGetValue(competitionId, out var teamNames)
                || !teamNames.TryGetValue(teamId, out var teamName))
            {
                logger.LogWarning("LigaInsider: Kickbase-Team {TeamId} nicht in der Liga-Tabelle gefunden - übersprungen.", teamId);
                continue;
            }

            if (!LigaInsiderTeamMap.TryGetTeamPage(teamName, out var teamPage))
            {
                logger.LogWarning("LigaInsider: kein Mapping für Team {TeamName} ({TeamId}) hinterlegt - übersprungen.", teamName, teamId);
                continue;
            }

            IReadOnlyList<LigaInsiderPlayerStatus> scraped;
            try
            {
                scraped = await ligaInsiderClient.GetPredictedLineupAsync(teamPage.Slug, teamPage.Id, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "LigaInsider: Scrape für {TeamName} fehlgeschlagen.", teamName);
                continue;
            }

            foreach (var player in group)
            {
                var match = scraped.FirstOrDefault(s => string.Equals(s.Name, player.Name, StringComparison.OrdinalIgnoreCase))
                    ?? scraped.FirstOrDefault(s =>
                        s.Name.Contains(player.Name, StringComparison.OrdinalIgnoreCase)
                        || player.Name.Contains(s.Name, StringComparison.OrdinalIgnoreCase));

                var newStatus = match is null
                    ? LineupStatus.Bench
                    : MapStatus(match.Status);
                var rawLabel = match?.RawIconLabel;

                if (match is null)
                {
                    logger.LogInformation(
                        "LigaInsider: {PlayerName} ({TeamName}) nicht in der Aufstellungs-Vorschau gefunden - als Bank eingestuft.",
                        player.Name, teamName);
                }

                var previous = await dbContext.LineupPredictions
                    .Where(l => l.PlayerId == player.Id)
                    .OrderByDescending(l => l.ScrapedAt)
                    .FirstOrDefaultAsync(ct);

                dbContext.LineupPredictions.Add(new LineupPrediction
                {
                    PlayerId = player.Id,
                    MatchdayNumber = 0, // Phase 4 MVP: nicht getrackt, ScrapedAt ist die verlässliche Reihenfolge
                    Status = newStatus,
                    Source = "LigaInsider",
                    ScrapedAt = now,
                });

                var statusChanged = previous is null ? newStatus != LineupStatus.Starting : previous.Status != newStatus;
                if (statusChanged && newStatus != LineupStatus.Starting)
                {
                    changedPlayers.Add((player, newStatus, rawLabel));
                }

                if (previous?.Status == LineupStatus.Starting && statusChanged && newStatus != LineupStatus.Starting)
                {
                    var label = string.IsNullOrEmpty(rawLabel) ? StatusLabel(newStatus) : rawLabel;
                    var reason = $"Laut LigaInsider nicht mehr in der Startelf ({label})";
                    if (await SellAlertHelper.TryCreateAsync(dbContext, player.Id, SellAlertSignal.LineupDowngrade, reason, ct))
                    {
                        newSellAlerts.Add((player, reason));
                    }
                }
            }
        }

        await dbContext.SaveChangesAsync(ct);

        if (changedPlayers.Count > 0)
        {
            await PushChangesAsync(changedPlayers, ct);
        }

        if (newSellAlerts.Count > 0)
        {
            await PushSellAlertsAsync(newSellAlerts, ct);
        }

        logger.LogInformation(
            "LigaInsider-Scrape fertig: {TeamCount} Teams, {PlayerCount} Spieler geprüft, {ChangeCount} Statusänderungen, {SellAlertCount} neue Verkaufs-Alarme.",
            playersByTeam.Count, squadPlayers.Count, changedPlayers.Count, newSellAlerts.Count);
    }

    private async Task PushSellAlertsAsync(List<(Player Player, string Reason)> alerts, CancellationToken ct)
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

    private async Task PushChangesAsync(
        List<(Player Player, LineupStatus NewStatus, string? RawLabel)> changedPlayers, CancellationToken ct)
    {
        var body = new StringBuilder();
        foreach (var (player, status, rawLabel) in changedPlayers)
        {
            var label = string.IsNullOrEmpty(rawLabel) ? StatusLabel(status) : rawLabel;
            body.AppendLine($"{player.Name}: {label}");
        }

        await ntfyPushService.SendAsync(
            title: "Aufstellungs-Wächter",
            message: body.ToString().TrimEnd(),
            tags: "warning,soccer",
            ct: ct);
    }

    private static LineupStatus MapStatus(LigaInsiderLineupStatus status) => status switch
    {
        LigaInsiderLineupStatus.Starting => LineupStatus.Starting,
        LigaInsiderLineupStatus.Doubtful => LineupStatus.Doubtful,
        LigaInsiderLineupStatus.Out => LineupStatus.Out,
        LigaInsiderLineupStatus.Bench => LineupStatus.Bench,
        _ => LineupStatus.Bench,
    };

    private static string StatusLabel(LineupStatus status) => status switch
    {
        LineupStatus.Bench => "vermutlich Bank",
        LineupStatus.Doubtful => "fraglich",
        LineupStatus.Out => "fehlt",
        _ => status.ToString(),
    };
}
