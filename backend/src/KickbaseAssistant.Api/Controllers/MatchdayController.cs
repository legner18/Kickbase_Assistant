using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api/leagues/{leagueId}/matchday")]
public class MatchdayController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> Get(string leagueId, [FromQuery] int? matchday, CancellationToken ct)
    {
        var league = await dbContext.Leagues.FindAsync([leagueId], ct);
        if (league is null)
        {
            return NotFound();
        }

        var competitionId = league.CompetitionId;
        var matches = await dbContext.LiveMatches
            .Where(match => match.CompetitionId == competitionId)
            .ToListAsync(ct);
        if (matches.Count == 0)
        {
            return Ok(new { matchdayNumber = (int?)null, availableMatchdays = Array.Empty<int>() });
        }

        var availableMatchdays = matches.Select(match => match.MatchdayNumber).Distinct().Order().ToList();
        var currentMatchday = matches
            .Where(match => match.Status != LiveMatchStatus.Finished)
            .Select(match => (int?)match.MatchdayNumber)
            .Min() ?? availableMatchdays[^1];
        var selectedMatchday = matchday is { } requested && availableMatchdays.Contains(requested) ? requested : currentMatchday;

        var odds = await dbContext.MatchOdds
            .Where(entry => entry.CompetitionId == competitionId)
            .ToDictionaryAsync(entry => entry.MatchId, ct);

        var teamNames = new Dictionary<string, (string? Name, string? Symbol)>();
        foreach (var match in matches)
        {
            teamNames[match.HomeTeamId] = (match.HomeTeamName ?? teamNames.GetValueOrDefault(match.HomeTeamId).Name, match.HomeTeamSymbol ?? teamNames.GetValueOrDefault(match.HomeTeamId).Symbol);
            teamNames[match.AwayTeamId] = (match.AwayTeamName ?? teamNames.GetValueOrDefault(match.AwayTeamId).Name, match.AwayTeamSymbol ?? teamNames.GetValueOrDefault(match.AwayTeamId).Symbol);
        }

        var selectedMatches = matches
            .Where(match => match.MatchdayNumber == selectedMatchday)
            .OrderBy(match => match.KickoffAt)
            .Select(match => new
            {
                matchId = match.Id,
                kickoffAt = match.KickoffAt,
                status = match.Status.ToString(),
                minute = match.Minute,
                homeTeamId = match.HomeTeamId,
                homeTeamName = match.HomeTeamName,
                homeTeamSymbol = match.HomeTeamSymbol,
                awayTeamId = match.AwayTeamId,
                awayTeamName = match.AwayTeamName,
                awayTeamSymbol = match.AwayTeamSymbol,
                homeGoals = match.HomeGoals,
                awayGoals = match.AwayGoals,
                homeTeamPoints = match.HomeTeamPoints,
                awayTeamPoints = match.AwayTeamPoints,
                odds = odds.TryGetValue(match.Id, out var matchOdds) ? MapOdds(matchOdds) : null,
            })
            .ToList();

        var teamPoints = matches
            .Where(match => match.PointsUpdatedAt != null)
            .SelectMany(match => new[]
            {
                (TeamId: match.HomeTeamId, match.MatchdayNumber, Points: match.HomeTeamPoints ?? 0),
                (TeamId: match.AwayTeamId, match.MatchdayNumber, Points: match.AwayTeamPoints ?? 0),
            })
            .GroupBy(entry => entry.TeamId)
            .Select(teamEntries => new
            {
                teamId = teamEntries.Key,
                teamName = teamNames.GetValueOrDefault(teamEntries.Key).Name,
                teamSymbol = teamNames.GetValueOrDefault(teamEntries.Key).Symbol,
                matchdayPoints = teamEntries.Where(entry => entry.MatchdayNumber == selectedMatchday).Select(entry => (int?)entry.Points).FirstOrDefault(),
                seasonPoints = teamEntries.Sum(entry => entry.Points),
                matchesCounted = teamEntries.Count(),
            })
            .OrderByDescending(team => team.matchdayPoints ?? int.MinValue)
            .ThenByDescending(team => team.seasonPoints)
            .ToList();

        var matchdayByMatchId = matches.ToDictionary(match => match.Id, match => match.MatchdayNumber);
        var matchIds = matchdayByMatchId.Keys.ToList();
        var playerPoints = await dbContext.LiveMatchPlayers
            .Where(player => matchIds.Contains(player.MatchId))
            .Select(player => new { player.MatchId, player.PlayerId, player.PlayerName, player.TeamId, player.Points })
            .ToListAsync(ct);
        var topPlayers = playerPoints
            .GroupBy(player => matchdayByMatchId[player.MatchId])
            .Select(matchdayPlayers => new { MatchdayNumber = matchdayPlayers.Key, Player = matchdayPlayers.MaxBy(player => player.Points)! })
            .OrderByDescending(entry => entry.MatchdayNumber)
            .ToList();
        var topPlayerIds = topPlayers.Select(entry => entry.Player.PlayerId).Distinct().ToList();
        var positions = await dbContext.Players
            .Where(player => topPlayerIds.Contains(player.Id))
            .ToDictionaryAsync(player => player.Id, player => player.Position, ct);
        var owners = await dbContext.SquadMemberships
            .Where(membership => membership.LeagueId == leagueId && topPlayerIds.Contains(membership.PlayerId))
            .Select(membership => new { membership.PlayerId, membership.ManagerName })
            .ToDictionaryAsync(membership => membership.PlayerId, membership => membership.ManagerName, ct);

        var fixtures = await dbContext.TeamFixtures
            .Where(fixture => fixture.CompetitionId == competitionId)
            .OrderBy(fixture => fixture.MatchdayNumber)
            .ToListAsync(ct);
        var matchByPairing = matches
            .GroupBy(match => (match.MatchdayNumber, match.HomeTeamId, match.AwayTeamId))
            .ToDictionary(pairing => pairing.Key, pairing => pairing.First());

        double? WinProbability(TeamFixture fixture)
        {
            var key = fixture.IsHome
                ? (fixture.MatchdayNumber, fixture.TeamId, fixture.OpponentTeamId)
                : (fixture.MatchdayNumber, fixture.OpponentTeamId, fixture.TeamId);
            if (!matchByPairing.TryGetValue(key, out var match) || !odds.TryGetValue(match.Id, out var matchOdds))
            {
                return null;
            }

            return fixture.IsHome ? matchOdds.HomeWinProbability : matchOdds.AwayWinProbability;
        }

        var remainingSchedule = fixtures
            .GroupBy(fixture => fixture.TeamId)
            .Select(teamFixtures => new
            {
                teamId = teamFixtures.Key,
                teamName = teamNames.GetValueOrDefault(teamFixtures.Key).Name,
                teamSymbol = teamNames.GetValueOrDefault(teamFixtures.Key).Symbol,
                averageDifficulty = teamFixtures.Average(fixture => fixture.DifficultyTier),
                fixtures = teamFixtures.Select(fixture => new
                {
                    matchdayNumber = fixture.MatchdayNumber,
                    opponentTeamId = fixture.OpponentTeamId,
                    opponentTeamName = fixture.OpponentTeamName ?? teamNames.GetValueOrDefault(fixture.OpponentTeamId).Name,
                    opponentSymbol = teamNames.GetValueOrDefault(fixture.OpponentTeamId).Symbol,
                    isHome = fixture.IsHome,
                    difficultyTier = fixture.DifficultyTier,
                    winProbability = WinProbability(fixture),
                }),
            })
            .OrderBy(team => team.averageDifficulty)
            .ThenBy(team => team.teamName)
            .ToList();

        return Ok(new
        {
            matchdayNumber = selectedMatchday,
            currentMatchday,
            availableMatchdays,
            hasOdds = odds.Count > 0,
            matches = selectedMatches,
            teamPoints,
            mvps = topPlayers.Select(entry => new
            {
                matchdayNumber = entry.MatchdayNumber,
                playerId = entry.Player.PlayerId,
                playerName = entry.Player.PlayerName,
                teamName = teamNames.GetValueOrDefault(entry.Player.TeamId).Name,
                position = positions.GetValueOrDefault(entry.Player.PlayerId),
                points = entry.Player.Points,
                ownerManagerName = owners.GetValueOrDefault(entry.Player.PlayerId),
            }),
            remainingSchedule,
        });
    }

    internal static object MapOdds(MatchOdds odds) => new
    {
        homeOdds = odds.HomeOdds,
        drawOdds = odds.DrawOdds,
        awayOdds = odds.AwayOdds,
        homeWinProbability = odds.HomeWinProbability,
        drawProbability = odds.DrawProbability,
        awayWinProbability = odds.AwayWinProbability,
        expectedHomeGoals = odds.ExpectedHomeGoals,
        expectedAwayGoals = odds.ExpectedAwayGoals,
        bookmakerCount = odds.BookmakerCount,
        updatedAt = odds.UpdatedAt,
    };
}
