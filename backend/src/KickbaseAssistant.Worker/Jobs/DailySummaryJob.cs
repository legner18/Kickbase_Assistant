using System.Text;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace KickbaseAssistant.Worker.Jobs;

/// <summary>
/// One consolidated daily push instead of only alert-triggered ones - a "what happened today"
/// digest, not another signal. Runs after the other evening jobs (22:30 Berlin, 15 min after
/// LeagueAnalysisJob) so today's snapshots/alerts are already settled.
///
/// SellAlert isn't league-scoped (predates the per-league SquadMembership model, like
/// Player.IsInSquad), so the "new alerts today" count is reported once globally; the squad-value
/// movement is reported per league since squad composition differs per league.
/// </summary>
public class DailySummaryJob(
    NtfyPushService ntfyPushService,
    AppDbContext dbContext,
    ILogger<DailySummaryJob> logger) : IJob
{
    private const int TopMoversPerLeague = 3;

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayStart = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var newAlertsToday = await dbContext.SellAlerts
            .CountAsync(a => a.CreatedAt >= todayStart && a.CreatedAt < todayStart.AddDays(1), ct);

        var activeLeagues = await dbContext.Leagues.Where(l => l.IsActive).ToListAsync(ct);
        var body = new StringBuilder();
        body.AppendLine(newAlertsToday > 0
            ? $"{newAlertsToday} neue(r) Verkaufs-Alarm(e) heute."
            : "Keine neuen Verkaufs-Alarme heute.");

        foreach (var league in activeLeagues)
        {
            var ownPlayerIds = await dbContext.SquadMemberships
                .Where(m => m.LeagueId == league.Id && m.ManagerId == league.OwnUserId)
                .Select(m => m.PlayerId)
                .ToListAsync(ct);

            var todaysChanges = await dbContext.MarketValueSnapshots
                .Where(s => ownPlayerIds.Contains(s.PlayerId) && s.Date == today)
                .Select(s => new { s.PlayerId, s.MarketValueChange, PlayerName = s.Player!.Name })
                .ToListAsync(ct);

            body.AppendLine();
            body.AppendLine($"{league.Name}:");

            if (todaysChanges.Count == 0)
            {
                body.AppendLine("Noch keine Marktwert-Daten für heute.");
                continue;
            }

            var total = todaysChanges.Sum(c => c.MarketValueChange);
            body.AppendLine($"Kaderwert {(total >= 0 ? "+" : "")}{total:N0} € heute");

            var top = todaysChanges.Where(c => c.MarketValueChange > 0).OrderByDescending(c => c.MarketValueChange).Take(TopMoversPerLeague);
            foreach (var mover in top)
            {
                body.AppendLine($"  + {mover.PlayerName}: +{mover.MarketValueChange:N0} €");
            }

            var flop = todaysChanges.Where(c => c.MarketValueChange < 0).OrderBy(c => c.MarketValueChange).Take(TopMoversPerLeague);
            foreach (var mover in flop)
            {
                body.AppendLine($"  - {mover.PlayerName}: {mover.MarketValueChange:N0} €");
            }
        }

        await ntfyPushService.SendAsync(
            title: "Tages-Update 📊",
            message: body.ToString().TrimEnd(),
            tags: "bar_chart",
            ct: ct);

        logger.LogInformation(
            "Tages-Zusammenfassung fertig: {LeagueCount} Ligen, {AlertCount} neue Alarme heute.",
            activeLeagues.Count, newAlertsToday);
    }
}
