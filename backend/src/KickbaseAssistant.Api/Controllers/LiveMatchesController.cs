using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api/leagues/{leagueId}/live-matches")]
public class LiveMatchesController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetCurrentMatchday(string leagueId, CancellationToken ct)
    {
        var league = await dbContext.Leagues.FindAsync([leagueId], ct);
        if (league is null)
        {
            return NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        var allMatches = await dbContext.LiveMatches
            .Where(match => match.CompetitionId == league.CompetitionId)
            .ToListAsync(ct);

        if (allMatches.Count == 0)
        {
            return Ok(new { matchday = (int?)null, nextKickoffAt = (DateTimeOffset?)null, matches = Array.Empty<object>() });
        }

        var currentMatchday = allMatches
            .Where(match => match.KickoffAt <= now)
            .Select(match => match.MatchdayNumber)
            .DefaultIfEmpty(allMatches.Min(match => match.MatchdayNumber))
            .Max();
        var nextKickoffAt = allMatches
            .Where(match => match.KickoffAt > now)
            .Select(match => (DateTimeOffset?)match.KickoffAt)
            .Min();

        var matches = allMatches
            .Where(match => match.MatchdayNumber == currentMatchday || match.Status == LiveMatchStatus.Live)
            .OrderBy(match => match.KickoffAt)
            .ThenBy(match => match.Id)
            .ToList();
        var matchIds = matches.Select(match => match.Id).ToList();

        var ownPlayerIds = await dbContext.SquadMemberships
            .Where(membership => membership.LeagueId == leagueId && membership.ManagerId == league.OwnUserId)
            .Select(membership => membership.PlayerId)
            .ToListAsync(ct);
        var ownPlayersByTeam = await dbContext.LiveMatchPlayers
            .Where(player => matchIds.Contains(player.MatchId) && ownPlayerIds.Contains(player.PlayerId))
            .GroupBy(player => new { player.MatchId, player.TeamId })
            .Select(group => new { group.Key.MatchId, group.Key.TeamId, Count = group.Count(), Points = group.Sum(player => player.Points) })
            .ToDictionaryAsync(entry => (entry.MatchId, entry.TeamId), ct);

        object BuildSide(LiveMatch match, string teamId, string? name, string? symbol, int? goals, int? points)
        {
            var ownPlayers = ownPlayersByTeam.GetValueOrDefault((match.Id, teamId));
            return new
            {
                teamId,
                name,
                symbol,
                goals,
                points,
                ownPlayerCount = ownPlayers?.Count ?? 0,
                ownPoints = ownPlayers?.Points ?? 0,
            };
        }

        return Ok(new
        {
            matchday = currentMatchday,
            nextKickoffAt,
            matches = matches.Select(match => new
            {
                matchId = match.Id,
                matchdayNumber = match.MatchdayNumber,
                kickoffAt = match.KickoffAt,
                status = match.Status,
                minute = match.Minute,
                pointsUpdatedAt = match.PointsUpdatedAt,
                home = BuildSide(match, match.HomeTeamId, match.HomeTeamName, match.HomeTeamSymbol, match.HomeGoals, match.HomeTeamPoints),
                away = BuildSide(match, match.AwayTeamId, match.AwayTeamName, match.AwayTeamSymbol, match.AwayGoals, match.AwayTeamPoints),
            }),
        });
    }

    [HttpGet("{matchId}/teams/{teamId}")]
    public async Task<ActionResult> GetTeamPoints(string leagueId, string matchId, string teamId, CancellationToken ct)
    {
        var league = await dbContext.Leagues.FindAsync([leagueId], ct);
        var match = await dbContext.LiveMatches.FindAsync([matchId], ct);
        if (league is null || match is null || (teamId != match.HomeTeamId && teamId != match.AwayTeamId))
        {
            return NotFound();
        }

        var isHome = teamId == match.HomeTeamId;
        var players = await dbContext.LiveMatchPlayers
            .Where(player => player.MatchId == matchId && player.TeamId == teamId)
            .ToListAsync(ct);
        var playerIds = players.Select(player => player.PlayerId).ToList();

        var owners = await dbContext.SquadMemberships
            .Where(membership => membership.LeagueId == leagueId && playerIds.Contains(membership.PlayerId))
            .Select(membership => new { membership.PlayerId, membership.ManagerId, membership.ManagerName })
            .ToListAsync(ct);
        var ownerByPlayer = owners
            .GroupBy(owner => owner.PlayerId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(owner => owner.ManagerId == league.OwnUserId).First());
        var positionByPlayer = await dbContext.Players
            .Where(player => playerIds.Contains(player.Id))
            .ToDictionaryAsync(player => player.Id, player => player.Position, ct);

        return Ok(new
        {
            matchId,
            teamId,
            teamName = isHome ? match.HomeTeamName : match.AwayTeamName,
            points = isHome ? match.HomeTeamPoints : match.AwayTeamPoints,
            pointsUpdatedAt = match.PointsUpdatedAt,
            players = players
                .OrderByDescending(player => player.Points)
                .ThenBy(player => player.PlayerName)
                .Select(player =>
                {
                    var owner = ownerByPlayer.GetValueOrDefault(player.PlayerId);
                    var hasPosition = positionByPlayer.TryGetValue(player.PlayerId, out var position);
                    return new
                    {
                        playerId = player.PlayerId,
                        playerName = player.PlayerName,
                        position = hasPosition ? position : (int?)null,
                        points = player.Points,
                        eventKeys = player.EventKeys,
                        ownerManagerName = owner?.ManagerName,
                        isMine = owner?.ManagerId == league.OwnUserId,
                        hasDetail = hasPosition,
                        hasTimeline = player.TimelineFetchedAt != null,
                    };
                }),
        });
    }

    [HttpGet("{matchId}/players/{playerId}/timeline")]
    public async Task<ActionResult> GetPlayerTimeline(string matchId, string playerId, CancellationToken ct)
    {
        var player = await dbContext.LiveMatchPlayers
            .Include(row => row.Events)
            .FirstOrDefaultAsync(row => row.MatchId == matchId && row.PlayerId == playerId, ct);
        if (player is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            matchId,
            playerId,
            playerName = player.PlayerName,
            points = player.Points,
            fetchedAt = player.TimelineFetchedAt,
            events = player.Events
                .OrderByDescending(matchEvent => matchEvent.Minute)
                .ThenByDescending(matchEvent => Math.Abs(matchEvent.Points))
                .Select(matchEvent => new
                {
                    eventId = matchEvent.EventId,
                    minute = matchEvent.Minute,
                    title = matchEvent.Title,
                    points = matchEvent.Points,
                }),
        });
    }
}
