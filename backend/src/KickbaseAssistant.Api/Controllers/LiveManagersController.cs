using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api/leagues/{leagueId}/live-managers")]
public class LiveManagersController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetStandings(string leagueId, CancellationToken ct)
    {
        var league = await dbContext.Leagues.FindAsync([leagueId], ct);
        if (league is null)
        {
            return NotFound();
        }

        var matchdayNumber = await dbContext.LiveManagerScores
            .Where(score => score.LeagueId == leagueId)
            .MaxAsync(score => (int?)score.MatchdayNumber, ct);
        if (matchdayNumber is null)
        {
            return Ok(new { matchday = (int?)null, updatedAt = (DateTimeOffset?)null, managers = Array.Empty<object>() });
        }

        var scores = await dbContext.LiveManagerScores
            .Include(score => score.Players)
            .Where(score => score.LeagueId == leagueId && score.MatchdayNumber == matchdayNumber)
            .ToListAsync(ct);

        var matchIds = scores
            .SelectMany(score => score.Players)
            .Where(player => player.MatchId is not null)
            .Select(player => player.MatchId!)
            .Distinct()
            .ToList();
        var matchesById = await dbContext.LiveMatches
            .Where(match => matchIds.Contains(match.Id))
            .ToDictionaryAsync(match => match.Id, ct);
        var livePlayers = await dbContext.LiveMatchPlayers
            .Where(player => matchIds.Contains(player.MatchId))
            .Select(player => new { player.MatchId, player.PlayerId, player.Points })
            .ToListAsync(ct);
        var pointsByPlayer = livePlayers.ToDictionary(player => (player.MatchId, player.PlayerId), player => player.Points);
        var now = DateTimeOffset.UtcNow;

        var standings = scores
            .Select(score => new
            {
                Score = score,
                Points = CalculateMatchdayPoints(score, score.Players
                    .Where(player => player.InLineup && player.MatchId is not null)
                    .Select(player => pointsByPlayer.GetValueOrDefault((player.MatchId!, player.PlayerId)))),
            })
            .OrderByDescending(standing => standing.Points)
            .ThenBy(standing => standing.Score.MatchdayRank)
            .ToList();

        return Ok(new
        {
            matchday = matchdayNumber,
            updatedAt = scores.Max(score => score.UpdatedAt),
            managers = standings
                .Select(standing =>
                {
                    var score = standing.Score;
                    var lineupMatches = score.Players
                        .Where(player => player.InLineup)
                        .Select(player => player.MatchId is not null ? matchesById.GetValueOrDefault(player.MatchId) : null)
                        .ToList();
                    return new
                    {
                        managerId = score.ManagerId,
                        managerName = score.ManagerName,
                        isMe = score.ManagerId == league.OwnUserId,
                        points = standing.Points,
                        pointsCounted = score.PointsCounted,
                        matchdayRank = standings.Count(other => other.Points > standing.Points) + 1,
                        seasonPoints = score.SeasonPoints,
                        seasonRank = score.SeasonRank,
                        lineupCount = lineupMatches.Count,
                        startedCount = lineupMatches.Count(match => match is not null && match.KickoffAt <= now),
                        liveCount = lineupMatches.Count(match => match?.Status == LiveMatchStatus.Live),
                    };
                }),
        });
    }

    [HttpGet("{managerId}")]
    public async Task<ActionResult> GetManager(string leagueId, string managerId, CancellationToken ct)
    {
        var league = await dbContext.Leagues.FindAsync([leagueId], ct);
        var score = await dbContext.LiveManagerScores
            .Include(row => row.Players)
            .Where(row => row.LeagueId == leagueId && row.ManagerId == managerId)
            .OrderByDescending(row => row.MatchdayNumber)
            .FirstOrDefaultAsync(ct);
        if (league is null || score is null)
        {
            return NotFound();
        }

        var playerIds = score.Players.Select(player => player.PlayerId).ToList();
        var matchIds = score.Players
            .Where(player => player.MatchId is not null)
            .Select(player => player.MatchId!)
            .Distinct()
            .ToList();

        var matchesById = await dbContext.LiveMatches
            .Where(match => matchIds.Contains(match.Id))
            .ToDictionaryAsync(match => match.Id, ct);
        var livePlayers = await dbContext.LiveMatchPlayers
            .Where(player => matchIds.Contains(player.MatchId) && playerIds.Contains(player.PlayerId))
            .Select(player => new
            {
                player.MatchId,
                player.PlayerId,
                player.Points,
                player.EventKeys,
                HasTimeline = player.TimelineFetchedAt != null,
            })
            .ToListAsync(ct);
        var livePlayerByKey = livePlayers.ToDictionary(player => (player.MatchId, player.PlayerId));
        var knownPlayers = await dbContext.Players
            .Where(player => playerIds.Contains(player.Id))
            .Select(player => new { player.Id, player.Position, player.TeamName })
            .ToDictionaryAsync(player => player.Id, ct);
        var now = DateTimeOffset.UtcNow;

        var players = score.Players
            .Select(player =>
            {
                var match = player.MatchId is not null ? matchesById.GetValueOrDefault(player.MatchId) : null;
                var livePlayer = player.MatchId is not null ? livePlayerByKey.GetValueOrDefault((player.MatchId, player.PlayerId)) : null;
                var known = knownPlayers.GetValueOrDefault(player.PlayerId);
                var isHome = match?.HomeTeamId == player.TeamId;
                var started = match is not null && match.KickoffAt <= now;

                return new
                {
                    playerId = player.PlayerId,
                    playerName = player.PlayerName,
                    position = known?.Position,
                    teamName = known?.TeamName ?? (isHome ? match?.HomeTeamName : match?.AwayTeamName),
                    inLineup = player.InLineup,
                    points = livePlayer?.Points ?? (started ? 0 : (int?)null),
                    eventKeys = livePlayer?.EventKeys ?? [],
                    hasTimeline = livePlayer?.HasTimeline ?? false,
                    hasDetail = known is not null,
                    match = match is null
                        ? null
                        : new
                        {
                            matchId = match.Id,
                            status = match.Status,
                            minute = match.Minute,
                            kickoffAt = match.KickoffAt,
                            opponent = isHome
                                ? match.AwayTeamName ?? match.AwayTeamSymbol
                                : match.HomeTeamName ?? match.HomeTeamSymbol,
                            isHome,
                        },
                };
            })
            .OrderByDescending(player => player.inLineup)
            .ThenByDescending(player => player.points ?? int.MinValue)
            .ThenBy(player => player.playerName)
            .ToList();

        return Ok(new
        {
            managerId = score.ManagerId,
            managerName = score.ManagerName,
            isMe = score.ManagerId == league.OwnUserId,
            matchday = score.MatchdayNumber,
            points = CalculateMatchdayPoints(score, players.Where(player => player.inLineup).Select(player => player.points ?? 0)),
            pointsCounted = score.PointsCounted,
            updatedAt = score.UpdatedAt,
            players,
        });
    }

    private static int CalculateMatchdayPoints(LiveManagerScore score, IEnumerable<int> lineupPoints) =>
        score.PointsCounted ? lineupPoints.Sum() : score.Points;
}
