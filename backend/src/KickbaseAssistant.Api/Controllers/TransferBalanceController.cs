using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api/leagues/{leagueId}/transfer-balance")]
public class TransferBalanceController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> Get(string leagueId, CancellationToken ct)
    {
        var league = await dbContext.Leagues.FindAsync([leagueId], ct);
        if (league is null)
        {
            return NotFound();
        }

        var transfers = await dbContext.LeagueTransfers
            .Where(transfer => transfer.LeagueId == leagueId && transfer.ManagerId == league.OwnUserId)
            .Select(transfer => new LedgerTransfer(transfer.PlayerId, transfer.PlayerName, transfer.Type, transfer.Price, transfer.OccurredAt))
            .ToListAsync(ct);

        var squad = await dbContext.SquadMemberships
            .Where(membership => membership.LeagueId == leagueId && membership.ManagerId == league.OwnUserId)
            .Select(membership => new LedgerSquadPlayer(membership.PlayerId, membership.Player!.Name, membership.Player.MarketValue))
            .ToListAsync(ct);

        var assignedValues = await dbContext.AssignedPlayerValues
            .Where(assigned => assigned.LeagueId == leagueId && assigned.ManagerId == league.OwnUserId)
            .ToDictionaryAsync(
                assigned => assigned.PlayerId,
                assigned => new AssignedValue(assigned.MarketValue, assigned.AssignedAt),
                ct);

        var ledger = TransferLedger.Build(transfers, squad, assignedValues);

        var buysWithOverpay = await dbContext.LeagueTransfers
            .Where(transfer => transfer.LeagueId == leagueId && transfer.Type == TransferType.Buy && transfer.OverpayPercent != null)
            .Select(transfer => new { transfer.ManagerId, OverpayPercent = transfer.OverpayPercent!.Value })
            .ToListAsync(ct);
        var ownOverpays = buysWithOverpay.Where(buy => buy.ManagerId == league.OwnUserId).Select(buy => buy.OverpayPercent).ToList();
        var otherOverpays = buysWithOverpay.Where(buy => buy.ManagerId != league.OwnUserId).Select(buy => buy.OverpayPercent).ToList();

        var closedByProfit = ledger.Closed.OrderByDescending(position => position.Profit).ToList();
        var openByProfit = ledger.Open.OrderByDescending(position => position.Profit).ToList();

        return Ok(new
        {
            realizedProfit = ledger.RealizedProfit,
            salesCount = ledger.Closed.Count,
            profitableSalesCount = ledger.ProfitableSalesCount,
            averageHeldDays = ledger.AverageHeldDays,
            unrealizedProfit = ledger.UnrealizedProfit,
            openPositionsCount = ledger.Open.Count,
            assignedCount = ledger.AssignedCount,
            assignedAt = assignedValues.Count > 0 ? assignedValues.Values.Min(assigned => assigned.AssignedAt) : (DateTimeOffset?)null,
            salesWithoutAcquisitionValue = ledger.SalesWithoutAcquisitionValue,
            squadPlayersWithoutAcquisitionValue = ledger.SquadPlayersWithoutAcquisitionValue,
            bestClosed = closedByProfit.Count > 0 ? MapClosed(closedByProfit[0]) : null,
            worstClosed = closedByProfit.Count > 0 ? MapClosed(closedByProfit[^1]) : null,
            bestOpen = openByProfit.Count > 0 ? MapOpen(openByProfit[0]) : null,
            worstOpen = openByProfit.Count > 0 ? MapOpen(openByProfit[^1]) : null,
            ownOverpay = new
            {
                averagePercent = ownOverpays.Count > 0 ? ownOverpays.Average() : (double?)null,
                count = ownOverpays.Count,
            },
            leagueOverpay = new
            {
                averagePercent = otherOverpays.Count > 0 ? otherOverpays.Average() : (double?)null,
                count = otherOverpays.Count,
            },
            closed = ledger.Closed.OrderByDescending(position => position.SoldAt).Select(MapClosed),
            open = openByProfit.Select(MapOpen),
        });
    }

    private static object MapClosed(ClosedPosition position) => new
    {
        playerId = position.PlayerId,
        playerName = position.PlayerName,
        acquisitionValue = position.AcquisitionValue,
        salePrice = position.SalePrice,
        profit = position.Profit,
        acquiredAt = position.AcquiredAt,
        soldAt = position.SoldAt,
        heldDays = position.HeldDays,
        wasAssigned = position.WasAssigned,
    };

    private static object MapOpen(OpenPosition position) => new
    {
        playerId = position.PlayerId,
        playerName = position.PlayerName,
        acquisitionValue = position.AcquisitionValue,
        marketValue = position.MarketValue,
        profit = position.Profit,
        acquiredAt = position.AcquiredAt,
        wasAssigned = position.WasAssigned,
    };
}
