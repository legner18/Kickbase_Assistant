using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api/leagues/{leagueId}/finance-check")]
public class FinanceCheckController(AppDbContext dbContext) : ControllerBase
{
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

        var teamValue = await dbContext.SquadMemberships
            .Where(membership => membership.LeagueId == leagueId && membership.ManagerId == league.OwnUserId)
            .SumAsync(membership => (double)membership.Player!.MarketValue, ct);

        var offers = await dbContext.OwnMarketOffers
            .Where(offer => offer.LeagueId == leagueId)
            .OrderBy(offer => offer.ExpiresAt)
            .Select(offer => new
            {
                playerId = offer.PlayerId,
                playerName = offer.PlayerName,
                offerPrice = offer.OfferPrice,
                marketValue = offer.MarketValue,
                listingPrice = offer.ListingPrice,
                expiresAt = offer.ExpiresAt,
                updatedAt = offer.UpdatedAt,
            })
            .ToListAsync(ct);

        var balance = balanceStatus.Balance;
        var creditLimit = BalanceGuardCalculator.CreditLimit(teamValue, balance);
        var availableToInvest = balance + creditLimit;
        var offersTotal = offers.Sum(offer => (double)offer.offerPrice);

        return Ok(new
        {
            balance,
            teamValue,
            creditLimit,
            availableToInvest,
            offers,
            offersTotal,
            balanceAfterOffers = balance - offersTotal,
            availableAfterOffers = availableToInvest - offersTotal,
            balanceUpdatedAt = balanceStatus.UpdatedAt,
        });
    }
}
