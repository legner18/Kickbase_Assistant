using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Players;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api/leagues/{leagueId}/fair-value-radar")]
public class FairValueRadarController(AppDbContext dbContext, PlayerMetricsService playerMetricsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> Get(string leagueId, CancellationToken ct)
    {
        var league = await dbContext.Leagues.FindAsync([leagueId], ct);
        if (league is null)
        {
            return NotFound();
        }

        var model = await playerMetricsService.ComputeFairValueModelAsync(ct);
        var playerIds = model.FairValues.Keys.ToList();

        var players = await dbContext.Players
            .Where(player => playerIds.Contains(player.Id))
            .Select(player => new
            {
                player.Id,
                player.Name,
                player.TeamName,
                player.Position,
                player.MarketValue,
                player.AveragePoints,
                player.TotalPoints,
            })
            .ToListAsync(ct);

        var owners = await dbContext.SquadMemberships
            .Where(membership => membership.LeagueId == leagueId)
            .Select(membership => new { membership.PlayerId, membership.ManagerId, membership.ManagerName })
            .ToListAsync(ct);
        var ownerByPlayerId = owners
            .GroupBy(owner => owner.PlayerId)
            .ToDictionary(playerOwners => playerOwners.Key, playerOwners => playerOwners.First());

        var marketPlayerIds = (await dbContext.MarketScanResults
                .Where(result => result.LeagueId == leagueId)
                .Select(result => result.PlayerId)
                .ToListAsync(ct))
            .Concat(await dbContext.OwnMarketOffers
                .Where(offer => offer.LeagueId == leagueId)
                .Select(offer => offer.PlayerId)
                .ToListAsync(ct))
            .ToHashSet();

        return Ok(new
        {
            slope = model.Slope,
            intercept = model.Intercept,
            players = players.Select(player =>
            {
                var fairValue = model.FairValues[player.Id];
                var owner = ownerByPlayerId.GetValueOrDefault(player.Id);
                return new
                {
                    id = player.Id,
                    name = player.Name,
                    teamName = player.TeamName,
                    position = player.Position,
                    marketValue = player.MarketValue,
                    averagePoints = player.AveragePoints,
                    totalPoints = player.TotalPoints,
                    fairValue,
                    fairValueDeltaPercent = player.MarketValue > 0 ? (fairValue - player.MarketValue) / player.MarketValue * 100 : 0,
                    ownerManagerName = owner?.ManagerName,
                    isOwn = owner?.ManagerId == league.OwnUserId,
                    isOnMarket = marketPlayerIds.Contains(player.Id),
                };
            }),
        });
    }
}
