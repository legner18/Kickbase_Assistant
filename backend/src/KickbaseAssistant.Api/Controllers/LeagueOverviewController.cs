using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api/leagues/{leagueId}/league-overview")]
public class LeagueOverviewController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> Get(string leagueId, CancellationToken ct)
    {
        var league = await dbContext.Leagues.FindAsync([leagueId], ct);
        if (league is null)
        {
            return NotFound();
        }

        var results = await dbContext.ManagerMatchdayResults
            .Where(result => result.LeagueId == leagueId)
            .ToListAsync(ct);
        var playedMatchdays = results
            .GroupBy(result => result.MatchdayNumber)
            .Where(matchdayGroup => matchdayGroup.Any(result => result.Points != 0))
            .Select(matchdayGroup => matchdayGroup.Key)
            .Order()
            .ToList();
        var playedResults = results.Where(result => playedMatchdays.Contains(result.MatchdayNumber)).ToList();

        var standings = RankHistoryCalculator.Compute(
            [.. playedResults.Select(result => new ManagerMatchdayPoints(result.ManagerId, result.MatchdayNumber, result.Points))]);

        var stats = await dbContext.LeagueManagerStats
            .Where(stat => stat.LeagueId == leagueId)
            .ToDictionaryAsync(stat => stat.ManagerId, ct);

        var squads = await dbContext.SquadMemberships
            .Where(membership => membership.LeagueId == leagueId)
            .Select(membership => new { membership.ManagerId, membership.PlayerId, membership.Player!.MarketValue })
            .ToListAsync(ct);
        var squadPlayerIds = squads.Select(member => member.PlayerId).Distinct().ToList();

        var latestDate = await dbContext.MarketValueSnapshots
            .Where(snapshot => squadPlayerIds.Contains(snapshot.PlayerId))
            .Select(snapshot => (DateOnly?)snapshot.Date)
            .MaxAsync(ct);
        var dailyChanges = new Dictionary<string, int>();
        if (latestDate is { } date)
        {
            var recentValues = await dbContext.MarketValueSnapshots
                .Where(snapshot => squadPlayerIds.Contains(snapshot.PlayerId)
                    && (snapshot.Date == date || snapshot.Date == date.AddDays(-1)))
                .Select(snapshot => new { snapshot.PlayerId, snapshot.Date, snapshot.MarketValue })
                .ToListAsync(ct);
            dailyChanges = recentValues
                .GroupBy(snapshot => snapshot.PlayerId)
                .Where(playerSnapshots => playerSnapshots.Count() == 2)
                .ToDictionary(
                    playerSnapshots => playerSnapshots.Key,
                    playerSnapshots => playerSnapshots.Single(snapshot => snapshot.Date == date).MarketValue
                        - playerSnapshots.Single(snapshot => snapshot.Date != date).MarketValue);
        }

        var managerNames = results
            .GroupBy(result => result.ManagerId)
            .ToDictionary(managerResults => managerResults.Key, managerResults => managerResults.OrderByDescending(result => result.MatchdayNumber).First().ManagerName);
        foreach (var stat in stats.Values)
        {
            managerNames[stat.ManagerId] = stat.ManagerName;
        }

        var managers = managerNames
            .Select(manager =>
            {
                var managerId = manager.Key;
                var squad = squads.Where(member => member.ManagerId == managerId).ToList();
                var coveredChanges = squad.Where(member => dailyChanges.ContainsKey(member.PlayerId)).ToList();
                var managerResults = playedResults.Where(result => result.ManagerId == managerId).ToList();
                var stat = stats.GetValueOrDefault(managerId);
                var managerStandings = standings.Where(standing => standing.ManagerId == managerId).OrderBy(standing => standing.MatchdayNumber).ToList();

                return new
                {
                    managerId,
                    managerName = manager.Value,
                    isMe = managerId == league.OwnUserId,
                    seasonPoints = stat?.SeasonPoints ?? managerStandings.LastOrDefault().CumulativePoints,
                    seasonRank = stat?.SeasonRank ?? managerStandings.LastOrDefault().Rank,
                    teamValue = squad.Sum(member => (double)member.MarketValue),
                    squadSize = squad.Count,
                    marketValueChangeToday = coveredChanges.Count > 0 ? coveredChanges.Sum(member => dailyChanges[member.PlayerId]) : (int?)null,
                    marketValueChangeCoverage = coveredChanges.Count,
                    averageMatchdayPoints = managerResults.Count > 0 ? managerResults.Average(result => result.Points) : (double?)null,
                    transferCount = stat?.TransferCount,
                    averageOverpayPercent = stat?.AverageOverpayPercent,
                    standings = managerStandings.Select(standing => new
                    {
                        matchdayNumber = standing.MatchdayNumber,
                        matchdayPoints = standing.MatchdayPoints,
                        cumulativePoints = standing.CumulativePoints,
                        rank = standing.Rank,
                    }),
                };
            })
            .OrderBy(manager => manager.seasonRank == 0 ? int.MaxValue : manager.seasonRank)
            .ThenBy(manager => manager.managerName)
            .ToList();

        return Ok(new
        {
            matchdays = playedMatchdays,
            marketValueDate = latestDate,
            managers,
        });
    }
}
