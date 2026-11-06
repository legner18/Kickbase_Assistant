using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Players;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api/leagues/{leagueId}/simulator")]
public class SimulatorController(AppDbContext dbContext, PlayerMetricsService playerMetricsService) : ControllerBase
{
    private const int LoginBonusPerDay = 100_000;
    private static readonly TimeZoneInfo GermanyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    [HttpGet]
    public async Task<ActionResult> Get(string leagueId, CancellationToken ct)
    {
        var league = await dbContext.Leagues.FindAsync([leagueId], ct);
        if (league is null)
        {
            return NotFound();
        }

        var balanceStatus = await dbContext.LeagueBalanceStatuses.FirstOrDefaultAsync(status => status.LeagueId == leagueId, ct);
        if (balanceStatus is null)
        {
            return NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        var updatesUntilKickoff = MarketValuePredictor.CountUpdatesUntil(now, balanceStatus.NextKickoffAt, GermanyTimeZone);
        var loginBonusDays = Math.Max(0, (TimeZoneInfo.ConvertTime(balanceStatus.NextKickoffAt, GermanyTimeZone).Date
            - TimeZoneInfo.ConvertTime(now, GermanyTimeZone).Date).Days);

        var squadPlayers = await dbContext.SquadMemberships
            .Where(membership => membership.LeagueId == leagueId && membership.ManagerId == league.OwnUserId)
            .Select(membership => membership.Player!)
            .ToListAsync(ct);
        var squadPlayerIds = squadPlayers.Select(player => player.Id).ToHashSet();

        var purchasePrices = await dbContext.LeagueTransfers
            .Where(transfer => transfer.LeagueId == leagueId && transfer.ManagerId == league.OwnUserId && transfer.Type == TransferType.Buy)
            .GroupBy(transfer => transfer.PlayerId)
            .Select(playerBuys => new
            {
                PlayerId = playerBuys.Key,
                Price = playerBuys.OrderByDescending(transfer => transfer.OccurredAt).First().Price,
            })
            .ToDictionaryAsync(buy => buy.PlayerId, buy => buy.Price, ct);

        var forecasts = (await playerMetricsService.ComputeForecastsAsync(
                squadPlayers.Select(player => (player.Id, player.MarketValue)), updatesUntilKickoff, ct))
            .ToDictionary(forecast => forecast.PlayerId);

        var ownOffers = await dbContext.OwnMarketOffers
            .Where(offer => offer.LeagueId == leagueId)
            .ToDictionaryAsync(offer => offer.PlayerId, ct);

        var scannedListings = await dbContext.MarketScanResults
            .Where(result => result.LeagueId == leagueId)
            .Select(result => new
            {
                playerId = result.PlayerId,
                playerName = result.Player!.Name,
                teamName = result.Player.TeamName,
                position = result.Player.Position,
                marketValue = result.Player.MarketValue,
                price = result.Price,
                expiresAt = result.ExpiresAt,
                ownerManagerName = dbContext.SquadMemberships
                    .Where(membership => membership.LeagueId == leagueId && membership.PlayerId == result.PlayerId)
                    .Select(membership => (string?)membership.ManagerName)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        var offeredPlayerIds = ownOffers.Keys.ToList();
        var offeredPlayers = await dbContext.Players
            .Where(player => offeredPlayerIds.Contains(player.Id))
            .ToDictionaryAsync(player => player.Id, ct);

        var market = scannedListings
            .Where(listing => !squadPlayerIds.Contains(listing.playerId))
            .Select(listing => new
            {
                listing.playerId,
                listing.playerName,
                listing.teamName,
                listing.position,
                listing.marketValue,
                listing.price,
                listing.expiresAt,
                listing.ownerManagerName,
                ownOfferPrice = ownOffers.TryGetValue(listing.playerId, out var offer) ? (int?)offer.OfferPrice : null,
            })
            .ToList();

        var scannedPlayerIds = market.Select(listing => listing.playerId).ToHashSet();
        foreach (var offer in ownOffers.Values.Where(offer => !scannedPlayerIds.Contains(offer.PlayerId)))
        {
            offeredPlayers.TryGetValue(offer.PlayerId, out var offeredPlayer);
            market.Add(new
            {
                playerId = offer.PlayerId,
                playerName = offer.PlayerName,
                teamName = offeredPlayer?.TeamName,
                position = offeredPlayer?.Position ?? 0,
                marketValue = offer.MarketValue,
                price = offer.ListingPrice,
                expiresAt = offer.ExpiresAt,
                ownerManagerName = (string?)null,
                ownOfferPrice = (int?)offer.OfferPrice,
            });
        }

        return Ok(new
        {
            balance = balanceStatus.Balance,
            nextMatchdayNumber = balanceStatus.NextMatchdayNumber,
            nextKickoffAt = balanceStatus.NextKickoffAt,
            updatesUntilKickoff,
            loginBonusDays,
            loginBonusPerDay = LoginBonusPerDay,
            minimumPositions = RosterCheck.Requirements.Select(requirement => new
            {
                position = requirement.Position,
                minCount = requirement.MinCount,
                label = requirement.Label,
            }),
            squad = squadPlayers
                .OrderByDescending(player => player.MarketValue)
                .Select(player =>
                {
                    var forecast = forecasts.GetValueOrDefault(player.Id);
                    return new
                    {
                        playerId = player.Id,
                        playerName = player.Name,
                        teamName = player.TeamName,
                        position = player.Position,
                        marketValue = player.MarketValue,
                        latestChange = forecast?.LatestChange,
                        predictedNextChange = forecast?.PredictedNextChange ?? 0,
                        predictedChangeUntilKickoff = forecast?.PredictedChangeUntilKickoff ?? 0,
                        purchasePrice = purchasePrices.TryGetValue(player.Id, out var price) ? (int?)price : null,
                    };
                }),
            market = market.OrderBy(listing => listing.expiresAt),
        });
    }
}
