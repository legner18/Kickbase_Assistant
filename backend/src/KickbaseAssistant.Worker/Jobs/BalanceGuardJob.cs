using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Kickbase;
using KickbaseAssistant.Infrastructure.Notifications;
using KickbaseAssistant.Infrastructure.Players;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace KickbaseAssistant.Worker.Jobs;

/// <summary>
/// Umbauplan "Kontostand-Wächter": verhindert 0 Punkte am Spieltag durch einen negativen
/// Kontostand beim Anpfiff. Rechnet Ampel-Status + Kreditrahmen (siehe BalanceGuardCalculator)
/// und schreibt das Ergebnis in LeagueBalanceStatus, aus der die Api liest - der Worker ist die
/// einzige Komponente, die Kickbase direkt anspricht.
///
/// Pusht (ntfy) einmalig pro (Liga, Spieltag, Fenster), wenn der Status beim T-24h- bzw.
/// T-3h-Fenster vor Anpfiff nicht grün ist - siehe BalanceAlertSent für die Dedup-Logik. Am
/// Vorabend des Spieltags (21 Uhr) trägt der Push zusätzlich eine Verkaufsplaner-Empfehlung
/// ("Deadline-Plan"), damit noch vor dem 22-Uhr-MW-Update reagiert werden kann.
/// </summary>
public class BalanceGuardJob(
    KickbaseApiClient kickbaseApiClient,
    NtfyPushService ntfyPushService,
    PlayerMetricsService playerMetricsService,
    AppDbContext dbContext,
    ILogger<BalanceGuardJob> logger) : IJob
{
    private static readonly TimeZoneInfo GermanyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private static readonly (BalanceAlertWindow Window, TimeSpan Threshold)[] AlertWindows =
    [
        (BalanceAlertWindow.TwentyFourHour, TimeSpan.FromHours(24)),
        (BalanceAlertWindow.ThreeHour, TimeSpan.FromHours(3)),
    ];

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var now = DateTimeOffset.UtcNow;
        var activeLeagues = await dbContext.Leagues.Where(l => l.IsActive).ToListAsync(ct);
        var checkedCount = 0;

        foreach (var league in activeLeagues)
        {
            try
            {
                await ProcessLeagueAsync(league, now, ct);
                checkedCount++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Kontostand-Wächter: Liga {LeagueName} konnte nicht geprüft werden.", league.Name);
            }
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Kontostand-Wächter fertig: {Count}/{Total} Ligen geprüft.", checkedCount, activeLeagues.Count);
    }

    private async Task ProcessLeagueAsync(League league, DateTimeOffset now, CancellationToken ct)
    {
        var budget = await kickbaseApiClient.GetMyBudgetAsync(league.Id, ct);
        var ranking = await kickbaseApiClient.GetRankingAsync(league.Id, ct);
        var me = ranking.Users.FirstOrDefault(u => u.UserId == league.OwnUserId);
        if (me is null)
        {
            return;
        }

        var matchdays = await kickbaseApiClient.GetMatchdaysAsync(league.CompetitionId, ct);
        var nextMatchday = matchdays
            .Where(md => md.Matches.Count > 0 && md.Matches.Min(m => m.KickoffAt) > now)
            .OrderBy(md => md.Day)
            .FirstOrDefault();
        if (nextMatchday is null)
        {
            return; // Saison zu Ende oder Spielplan noch nicht veröffentlicht.
        }

        var kickoff = nextMatchday.Matches.Min(m => m.KickoffAt);

        var squadPlayers = await dbContext.SquadMemberships
            .Where(m => m.LeagueId == league.Id && m.ManagerId == league.OwnUserId)
            .Select(m => m.Player!)
            .ToListAsync(ct);

        var positiveBuffer = 0.0;
        foreach (var player in squadPlayers)
        {
            var latest = await dbContext.MarketValueSnapshots
                .Where(s => s.PlayerId == player.Id)
                .OrderByDescending(s => s.Date)
                .FirstOrDefaultAsync(ct);
            if (latest is not null && latest.MarketValueChange > 0)
            {
                positiveBuffer += latest.MarketValueChange;
            }
        }

        var creditLimit = BalanceGuardCalculator.CreditLimit(me.TeamValue, budget.Budget);
        var status = BalanceGuardCalculator.GetStatus(budget.Budget, positiveBuffer);
        var gaps = RosterCheck.FindGaps(squadPlayers.Select(p => p.Position).ToList());

        var statusRow = await dbContext.LeagueBalanceStatuses.FirstOrDefaultAsync(s => s.LeagueId == league.Id, ct);
        if (statusRow is null)
        {
            statusRow = new LeagueBalanceStatus { LeagueId = league.Id };
            dbContext.LeagueBalanceStatuses.Add(statusRow);
        }

        statusRow.Balance = budget.Budget;
        statusRow.CreditLimit = creditLimit;
        statusRow.Status = status;
        statusRow.NextMatchdayNumber = nextMatchday.Day;
        statusRow.NextKickoffAt = kickoff;
        statusRow.RosterGaps = [.. gaps];
        statusRow.UpdatedAt = DateTimeOffset.UtcNow;

        if (status != BalanceStatus.Green)
        {
            var timeToKickoff = kickoff - now;
            foreach (var (window, threshold) in AlertWindows)
            {
                if (timeToKickoff <= threshold)
                {
                    await TrySendAlertAsync(league, nextMatchday.Day, window, status, budget.Budget, kickoff, ct);
                }
            }

            if (IsDeadlinePlanHour(now, kickoff))
            {
                await TrySendDeadlinePlanAsync(league, nextMatchday.Day, squadPlayers, budget.Budget, kickoff, ct);
            }
        }
    }

    private static bool IsDeadlinePlanHour(DateTimeOffset now, DateTimeOffset kickoff)
    {
        var nowLocal = TimeZoneInfo.ConvertTime(now, GermanyTimeZone);
        var kickoffLocal = TimeZoneInfo.ConvertTime(kickoff, GermanyTimeZone);
        return kickoffLocal.Date == nowLocal.Date.AddDays(1) && nowLocal.Hour == 21;
    }

    private async Task TrySendDeadlinePlanAsync(
        League league, int matchdayNumber, List<Player> squadPlayers, double balance, DateTimeOffset kickoff, CancellationToken ct)
    {
        var alreadySent = await dbContext.BalanceAlertsSent.AnyAsync(
            a => a.LeagueId == league.Id && a.MatchdayNumber == matchdayNumber && a.Window == BalanceAlertWindow.DeadlinePlan, ct);
        if (alreadySent)
        {
            return;
        }

        dbContext.BalanceAlertsSent.Add(new BalanceAlertSent
        {
            LeagueId = league.Id,
            MatchdayNumber = matchdayNumber,
            Window = BalanceAlertWindow.DeadlinePlan,
            SentAt = DateTimeOffset.UtcNow,
        });

        var lockedIds = (await dbContext.PlayerSaleLocks.Where(l => l.LeagueId == league.Id).Select(l => l.PlayerId).ToListAsync(ct)).ToHashSet();
        var candidates = new List<SellCandidate>();
        foreach (var player in squadPlayers)
        {
            var expectedPoints = await playerMetricsService.ComputeExpectedPointsAsync(player.Id, player.AveragePoints, ct);
            candidates.Add(new SellCandidate(player.Id, player.Name, player.Position, player.MarketValue, expectedPoints, lockedIds.Contains(player.Id)));
        }

        var best = SellPlanOptimizer.FindBestCombinations(candidates, balance, targetBalance: 0, maxResults: 1).FirstOrDefault();

        string message;
        if (best.PlayerIds is null || best.PlayerIds.Count == 0)
        {
            message = best.PlayerIds is null
                ? "Keine Verkaufs-Kombination gefunden, die den Kontostand ausgleicht, ohne die Mindestbesetzung zu verletzen - Kader prüfen."
                : "Kontostand reicht bereits aus, kein Verkauf nötig.";
        }
        else
        {
            var names = string.Join(", ", best.PlayerIds.Select(id => candidates.First(c => c.PlayerId == id).PlayerName));
            message = $"Verkaufe {names} ({best.Proceeds:+#,0 €;-#,0 €;0 €}) -> Kontostand danach {best.ResultingBalance:+#,0 €;-#,0 €;0 €}, {best.LostExpectedPoints:0.#} erwartete Punkte weniger.";
        }

        await ntfyPushService.SendAsync(
            title: $"Deadline-Plan {league.Name} - vor dem 22-Uhr-Update verkaufen ⏰",
            message: message,
            tags: "hourglass_flowing_sand,money_with_wings",
            ct: ct);
    }

    private async Task TrySendAlertAsync(
        League league, int matchdayNumber, BalanceAlertWindow window, BalanceStatus status, double balance, DateTimeOffset kickoff, CancellationToken ct)
    {
        var alreadySent = await dbContext.BalanceAlertsSent.AnyAsync(
            a => a.LeagueId == league.Id && a.MatchdayNumber == matchdayNumber && a.Window == window, ct);
        if (alreadySent)
        {
            return;
        }

        dbContext.BalanceAlertsSent.Add(new BalanceAlertSent
        {
            LeagueId = league.Id,
            MatchdayNumber = matchdayNumber,
            Window = window,
            SentAt = DateTimeOffset.UtcNow,
        });

        var windowLabel = window == BalanceAlertWindow.TwentyFourHour ? "24 Stunden" : "3 Stunden";
        var statusLabel = status == BalanceStatus.Red ? "ROT" : "GELB";
        await ntfyPushService.SendAsync(
            title: $"Kontostand {statusLabel} - noch {windowLabel} bis Anpfiff ⚠️",
            message: $"{league.Name}: Kontostand {balance:+#,0 €;-#,0 €;0 €} - Anpfiff {kickoff:dd.MM. HH:mm} Uhr. Bei Minus zum Spieltagsbeginn gibt es 0 Punkte.",
            tags: "rotating_light,money_with_wings",
            ct: ct);
    }
}
