using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api/leagues/{leagueId}/market-report")]
public class MarketReportController(AppDbContext dbContext) : ControllerBase
{
    private const int HistoryDays = 14;
    private const int WeekDays = 7;
    private const int TopCount = 5;

    [HttpGet]
    public async Task<ActionResult> Get(string leagueId, [FromQuery] int? matchday, CancellationToken ct)
    {
        var league = await dbContext.Leagues.FindAsync([leagueId], ct);
        if (league is null)
        {
            return NotFound();
        }

        var competitionNumber = int.TryParse(league.CompetitionId, out var parsed) ? parsed : 0;
        var competitionSnapshots = dbContext.MarketValueSnapshots
            .Where(snapshot => snapshot.Player!.CompetitionId == competitionNumber);

        var latestDate = await competitionSnapshots
            .Select(snapshot => (DateOnly?)snapshot.Date)
            .MaxAsync(ct);

        List<MarketValuePoint> points = latestDate is null
            ? []
            : await competitionSnapshots
                .Where(snapshot => snapshot.Date >= latestDate.Value.AddDays(-HistoryDays))
                .Select(snapshot => new MarketValuePoint(snapshot.PlayerId, snapshot.Date, snapshot.MarketValue))
                .ToListAsync(ct);

        var movements = MarketReportCalculator.ComputeDailyMovements(points);
        var latestMovement = movements.Count > 0 && movements[^1].Date == latestDate ? movements[^1] : (DailyMarketMovement?)null;

        IReadOnlyList<PlayerValueChange> dailyChanges = latestMovement is null || latestDate is null
            ? []
            : MarketReportCalculator.ComputeChanges(points, latestDate.Value, 1);
        IReadOnlyList<PlayerValueChange> weeklyChanges = latestDate is null
            ? []
            : MarketReportCalculator.ComputeChanges(points, latestDate.Value, WeekDays);
        var hasWeeklyCoverage = latestMovement is { } coverage && weeklyChanges.Count >= coverage.PlayerCount * MarketReportCalculator.MinimumCoverageShare;
        if (!hasWeeklyCoverage)
        {
            weeklyChanges = [];
        }

        var dailyWinners = dailyChanges.Where(change => change.Change > 0).OrderByDescending(change => change.Change).Take(TopCount).ToList();
        var dailyLosers = dailyChanges.Where(change => change.Change < 0).OrderBy(change => change.Change).Take(TopCount).ToList();
        var weeklyWinners = weeklyChanges.Where(change => change.Change > 0).OrderByDescending(change => change.Change).Take(TopCount).ToList();
        var weeklyLosers = weeklyChanges.Where(change => change.Change < 0).OrderBy(change => change.Change).Take(TopCount).ToList();

        var topEleven = await BuildTopElevenAsync(league.CompetitionId, matchday, ct);

        var involvedPlayerIds = dailyWinners.Concat(dailyLosers).Concat(weeklyWinners).Concat(weeklyLosers)
            .Select(change => change.PlayerId)
            .Concat(topEleven?.Players.Select(player => player.PlayerId) ?? Enumerable.Empty<string>())
            .Distinct()
            .ToList();
        var playerInfo = await dbContext.Players
            .Where(player => involvedPlayerIds.Contains(player.Id))
            .Select(player => new { player.Id, player.Name, player.TeamName, player.Position })
            .ToDictionaryAsync(player => player.Id, ct);
        var owners = await dbContext.SquadMemberships
            .Where(membership => membership.LeagueId == leagueId && involvedPlayerIds.Contains(membership.PlayerId))
            .Select(membership => new { membership.PlayerId, membership.ManagerName })
            .ToDictionaryAsync(membership => membership.PlayerId, membership => membership.ManagerName, ct);

        object MapChange(PlayerValueChange change)
        {
            var info = playerInfo.GetValueOrDefault(change.PlayerId);
            return new
            {
                playerId = change.PlayerId,
                playerName = info?.Name ?? change.PlayerId,
                teamName = info?.TeamName,
                position = info?.Position ?? 0,
                marketValue = change.MarketValue,
                change = change.Change,
                ownerManagerName = owners.GetValueOrDefault(change.PlayerId),
            };
        }

        var bestDay = movements.Count > 0 ? movements.MaxBy(movement => movement.TotalChange) : (DailyMarketMovement?)null;
        var worstDay = movements.Count > 0 ? movements.MinBy(movement => movement.TotalChange) : (DailyMarketMovement?)null;

        return Ok(new
        {
            latestDate,
            today = latestMovement is { } todayMovement
                ? new
                {
                    totalChange = todayMovement.TotalChange,
                    risingCount = todayMovement.RisingCount,
                    fallingCount = todayMovement.FallingCount,
                    unchangedCount = todayMovement.UnchangedCount,
                }
                : null,
            streak = MarketReportCalculator.CountStreak(movements),
            bestDay = bestDay is { } best ? new { date = best.Date, totalChange = best.TotalChange } : null,
            worstDay = worstDay is { } worst ? new { date = worst.Date, totalChange = worst.TotalChange } : null,
            history = movements.Select(movement => new
            {
                date = movement.Date,
                totalChange = movement.TotalChange,
                risingCount = movement.RisingCount,
                fallingCount = movement.FallingCount,
                playerCount = movement.PlayerCount,
            }),
            dailyWinners = dailyWinners.Select(MapChange),
            dailyLosers = dailyLosers.Select(MapChange),
            weeklyWinners = weeklyWinners.Select(MapChange),
            weeklyLosers = weeklyLosers.Select(MapChange),
            topEleven = topEleven is null
                ? null
                : new
                {
                    matchdayNumber = topEleven.MatchdayNumber,
                    availableMatchdays = topEleven.AvailableMatchdays,
                    formation = topEleven.Formation,
                    totalPoints = topEleven.TotalPoints,
                    candidateCount = topEleven.CandidateCount,
                    players = topEleven.Players.Select(player =>
                    {
                        var info = playerInfo.GetValueOrDefault(player.PlayerId);
                        return new
                        {
                            playerId = player.PlayerId,
                            playerName = info?.Name ?? player.PlayerName,
                            teamName = info?.TeamName,
                            position = player.Position,
                            points = player.Points,
                            isMvp = player.IsMvp,
                            ownerManagerName = owners.GetValueOrDefault(player.PlayerId),
                        };
                    }),
                },
        });
    }

    private record TopElevenPlayer(string PlayerId, string PlayerName, int Position, int Points, bool IsMvp);

    private record TopEleven(
        int MatchdayNumber,
        IReadOnlyList<int> AvailableMatchdays,
        string Formation,
        int TotalPoints,
        int CandidateCount,
        IReadOnlyList<TopElevenPlayer> Players);

    private async Task<TopEleven?> BuildTopElevenAsync(string competitionId, int? requestedMatchday, CancellationToken ct)
    {
        var matchStatuses = await dbContext.LiveMatches
            .Where(match => match.CompetitionId == competitionId)
            .Select(match => new { match.MatchdayNumber, match.Status })
            .ToListAsync(ct);

        var finishedMatchdays = matchStatuses
            .GroupBy(match => match.MatchdayNumber)
            .Where(matchdayGroup => matchdayGroup.All(match => match.Status == LiveMatchStatus.Finished))
            .Select(matchdayGroup => matchdayGroup.Key)
            .OrderBy(matchdayNumber => matchdayNumber)
            .ToList();
        if (finishedMatchdays.Count == 0)
        {
            return null;
        }

        var matchdayNumber = requestedMatchday is { } requested && finishedMatchdays.Contains(requested)
            ? requested
            : finishedMatchdays[^1];

        var matchdayPlayers = await dbContext.LiveMatchPlayers
            .Where(player => player.Match!.CompetitionId == competitionId && player.Match.MatchdayNumber == matchdayNumber)
            .Select(player => new { player.PlayerId, player.PlayerName, player.Points })
            .ToListAsync(ct);
        var matchdayPlayerIds = matchdayPlayers.Select(player => player.PlayerId).Distinct().ToList();
        var positions = await dbContext.Players
            .Where(player => matchdayPlayerIds.Contains(player.Id))
            .ToDictionaryAsync(player => player.Id, player => player.Position, ct);

        var candidates = matchdayPlayers
            .Where(player => positions.GetValueOrDefault(player.PlayerId) is >= 1 and <= 4)
            .GroupBy(player => player.PlayerId)
            .Select(playerGroup => playerGroup.OrderByDescending(player => player.Points).First())
            .Select(player => new LineupCandidate(
                player.PlayerId, player.PlayerName, positions[player.PlayerId], player.Points, 1, false))
            .ToList();

        var lineup = LineupOptimizer.FindBestLineup(candidates);
        if (lineup is not { } bestLineup)
        {
            return null;
        }

        var candidatesById = candidates.ToDictionary(candidate => candidate.PlayerId);
        var selected = bestLineup.PlayerIds.Select(playerId => candidatesById[playerId]).ToList();
        var mvpPoints = selected.Max(candidate => candidate.ExpectedPoints);
        var mvpId = selected.First(candidate => candidate.ExpectedPoints == mvpPoints).PlayerId;

        return new TopEleven(
            matchdayNumber,
            finishedMatchdays,
            bestLineup.Formation.ToString(),
            (int)bestLineup.TotalExpectedPoints,
            candidates.Count,
            [.. selected
                .OrderBy(candidate => candidate.Position)
                .ThenByDescending(candidate => candidate.ExpectedPoints)
                .Select(candidate => new TopElevenPlayer(
                    candidate.PlayerId, candidate.PlayerName, candidate.Position, (int)candidate.ExpectedPoints, candidate.PlayerId == mvpId))]);
    }
}
