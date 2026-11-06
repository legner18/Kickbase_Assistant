using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Kickbase;
using KickbaseAssistant.Infrastructure.Kickbase.Dtos;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace KickbaseAssistant.Worker.Jobs;

[DisallowConcurrentExecution]
public class MatchPointsBackfillJob(
    KickbaseApiClient kickbaseApiClient,
    AppDbContext dbContext,
    ILogger<MatchPointsBackfillJob> logger) : IJob
{
    private const int MatchesPerRun = 3;
    private static readonly TimeSpan LiveJobWindow = TimeSpan.FromDays(7);
    private static bool isDisabledUntilRestart;

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        if (isDisabledUntilRestart)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var olderThan = now - LiveJobWindow;

        var matches = await dbContext.LiveMatches
            .Include(match => match.Players)
            .Where(match => match.Status == LiveMatchStatus.Finished && match.PointsUpdatedAt == null && match.KickoffAt <= olderThan)
            .OrderBy(match => match.KickoffAt)
            .Take(MatchesPerRun)
            .ToListAsync(ct);
        if (matches.Count == 0)
        {
            return;
        }

        var backfilledCount = 0;
        foreach (var match in matches)
        {
            KickbaseTeamCenterDto home;
            KickbaseTeamCenterDto away;
            try
            {
                home = await kickbaseApiClient.GetCompetitionTeamCenterForMatchdayAsync(match.CompetitionId, match.HomeTeamId, match.MatchdayNumber, ct);
                away = await kickbaseApiClient.GetCompetitionTeamCenterForMatchdayAsync(match.CompetitionId, match.AwayTeamId, match.MatchdayNumber, ct);
            }
            catch (Exception ex) when (KickbaseApiClient.IsBlockedResponse(ex))
            {
                logger.LogWarning(ex, "Spieltags-Nachladen: Kickbase blockt Anfragen - abgebrochen.");
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Spieltags-Nachladen: Spiel {MatchId} konnte nicht geladen werden.", match.Id);
                continue;
            }

            var reportedMatchId = home.Match?.MatchId ?? away.Match?.MatchId;
            if (reportedMatchId != match.Id)
            {
                isDisabledUntilRestart = true;
                logger.LogWarning(
                    "Spieltags-Nachladen: Teamcenter für Spieltag {Matchday} meldet Spiel {ReportedMatchId} statt {MatchId} - bis zum Neustart deaktiviert.",
                    match.MatchdayNumber, reportedMatchId ?? "keins", match.Id);
                break;
            }

            match.HomeTeamName = home.TeamName;
            match.AwayTeamName = away.TeamName;
            match.HomeTeamPoints = home.Points;
            match.AwayTeamPoints = away.Points;
            match.PointsUpdatedAt = now;

            dbContext.LiveMatchPlayers.RemoveRange(match.Players);
            AddPlayers(match, match.HomeTeamId, home.Players);
            AddPlayers(match, match.AwayTeamId, away.Players);
            backfilledCount++;
        }

        await dbContext.SaveChangesAsync(ct);

        if (backfilledCount > 0)
        {
            logger.LogInformation("Spieltags-Nachladen: Punkte für {Count} ältere(s) Spiel(e) ergänzt.", backfilledCount);
        }
    }

    private void AddPlayers(LiveMatch match, string teamId, IReadOnlyList<KickbaseTeamCenterPlayerDto> players)
    {
        foreach (var player in players)
        {
            dbContext.LiveMatchPlayers.Add(new LiveMatchPlayer
            {
                MatchId = match.Id,
                TeamId = teamId,
                PlayerId = player.Id,
                PlayerName = player.Name,
                Points = player.Points,
                EventKeys = [.. player.EventKeys],
            });
        }
    }
}
