using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Kickbase;
using KickbaseAssistant.Infrastructure.Notifications;
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
/// T-3h-Fenster vor Anpfiff nicht grün ist - siehe BalanceAlertSent für die Dedup-Logik.
/// </summary>
public class BalanceGuardJob(
    KickbaseApiClient kickbaseApiClient,
    NtfyPushService ntfyPushService,
    AppDbContext dbContext,
    ILogger<BalanceGuardJob> logger) : IJob
{
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
        }
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
