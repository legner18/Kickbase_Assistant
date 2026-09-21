using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Kickbase;
using KickbaseAssistant.Infrastructure.Kickbase.Dtos;
using KickbaseAssistant.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace KickbaseAssistant.Worker.Jobs;

/// <summary>
/// Phase 6 (Live-Punkte): during typical Bundesliga kickoff windows, polls each active league's
/// ranking every 90s and - only while Kickbase itself reports a match live (the "il" flag) -
/// pushes the standings to the Api's SignalR hub, which re-broadcasts to connected clients.
///
/// The "typical windows" check (Fri evening / Sat + Sun afternoon-evening, Europe/Berlin) is a
/// deliberately simple stand-in for querying exact kickoff times per matchday: it covers the
/// vast majority of Bundesliga fixtures without any extra API calls, at the cost of missing rare
/// mid-week (e.g. cup) matches. Outside those windows the job does nothing at all - no request is
/// sent, honouring "poll only during matches, not at all outside them" from the plan even though
/// the trigger itself fires unconditionally every 90s.
/// </summary>
public class LiveScoreJob(
    KickbaseApiClient kickbaseApiClient,
    LiveHubClient liveHubClient,
    AppDbContext dbContext,
    ILogger<LiveScoreJob> logger) : IJob
{
    private static readonly TimeZoneInfo GermanyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        if (!IsWithinTypicalMatchWindow(DateTimeOffset.UtcNow))
        {
            return;
        }

        var activeLeagues = await dbContext.Leagues.Where(l => l.IsActive).ToListAsync(ct);
        var liveCount = 0;

        foreach (var league in activeLeagues)
        {
            KickbaseRankingDto ranking;
            try
            {
                ranking = await kickbaseApiClient.GetRankingAsync(league.Id, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Live-Ranking für Liga {LeagueName} konnte nicht geladen werden.", league.Name);
                continue;
            }

            if (!ranking.IsLive)
            {
                continue;
            }

            liveCount++;
            var payload = new
            {
                leagueId = league.Id,
                leagueName = league.Name,
                matchday = ranking.CurrentMatchday,
                standings = ranking.Users
                    .OrderBy(u => u.MatchdayRank)
                    .Select(u => new
                    {
                        userId = u.UserId,
                        name = u.Name,
                        seasonPoints = u.SeasonPoints,
                        matchdayPoints = u.MatchdayPoints,
                        seasonRank = u.SeasonRank,
                        matchdayRank = u.MatchdayRank,
                    }),
            };

            await liveHubClient.PublishRankingAsync(league.Id, payload, ct);
        }

        logger.LogInformation(
            "Live-Score-Check fertig: {LeagueCount} Ligen geprüft, {LiveCount} davon live.",
            activeLeagues.Count, liveCount);
    }

    private static bool IsWithinTypicalMatchWindow(DateTimeOffset utcNow)
    {
        var local = TimeZoneInfo.ConvertTime(utcNow, GermanyTimeZone);
        var hour = local.Hour;

        return local.DayOfWeek switch
        {
            DayOfWeek.Friday => hour is >= 18 and < 22,
            DayOfWeek.Saturday => hour is >= 14 and < 20,
            DayOfWeek.Sunday => hour is >= 14 and < 21,
            _ => false,
        };
    }
}
