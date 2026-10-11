using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Players;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api/leagues/{leagueId}/sell-plan")]
public class SellPlanController(AppDbContext dbContext, PlayerMetricsService playerMetricsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> Get(string leagueId, CancellationToken ct)
    {
        var league = await dbContext.Leagues.FindAsync([leagueId], ct);
        if (league is null)
        {
            return NotFound();
        }

        var squadPlayers = await dbContext.SquadMemberships
            .Where(m => m.LeagueId == leagueId && m.ManagerId == league.OwnUserId)
            .Select(m => m.Player!)
            .ToListAsync(ct);

        var lockedIds = (await dbContext.PlayerSaleLocks
            .Where(l => l.LeagueId == leagueId)
            .Select(l => l.PlayerId)
            .ToListAsync(ct)).ToHashSet();

        // Nur Anzeige (siehe GetSquad-Kommentar) - nie Teil der Optimierer-Eingabe (SellCandidate),
        // damit Kaufpreis/Gewinn-Verlust gar nicht erst in die Zielfunktion gelangen kann.
        var purchasePrices = await dbContext.LeagueTransfers
            .Where(t => t.LeagueId == leagueId && t.ManagerId == league.OwnUserId && t.Type == TransferType.Buy)
            .GroupBy(t => t.PlayerId)
            .Select(g => new { PlayerId = g.Key, Price = g.OrderByDescending(t => t.OccurredAt).First().Price })
            .ToDictionaryAsync(x => x.PlayerId, x => x.Price, ct);

        var candidates = new List<SellCandidate>();
        foreach (var player in squadPlayers)
        {
            var expectedPoints = await playerMetricsService.ComputeExpectedPointsAsync(player.Id, player.AveragePoints, ct);
            candidates.Add(new SellCandidate(player.Id, player.Name, player.Position, player.MarketValue, expectedPoints, lockedIds.Contains(player.Id)));
        }

        var balanceStatus = await dbContext.LeagueBalanceStatuses.FirstOrDefaultAsync(s => s.LeagueId == leagueId, ct);
        var balance = balanceStatus?.Balance ?? 0;

        var combinations = SellPlanOptimizer.FindBestCombinations(candidates, balance, targetBalance: 0);

        return Ok(new
        {
            balance,
            candidates = candidates.Select(c => new
            {
                playerId = c.PlayerId,
                playerName = c.PlayerName,
                position = c.Position,
                marketValue = c.MarketValue,
                expectedPoints = c.ExpectedPoints,
                locked = c.Locked,
                purchasePrice = purchasePrices.TryGetValue(c.PlayerId, out var price) ? (int?)price : null,
                profitLoss = purchasePrices.TryGetValue(c.PlayerId, out var priceForDelta) ? (int?)(c.MarketValue - priceForDelta) : null,
            }),
            combinations = combinations.Select(combo => new
            {
                playerIds = combo.PlayerIds,
                playerNames = combo.PlayerIds.Select(id => candidates.First(c => c.PlayerId == id).PlayerName),
                proceeds = combo.Proceeds,
                lostExpectedPoints = combo.LostExpectedPoints,
                remainingSquadSize = combo.RemainingSquadSize,
                resultingBalance = combo.ResultingBalance,
            }),
        });
    }

    [HttpPost("lock/{playerId}")]
    public async Task<ActionResult> ToggleLock(string leagueId, string playerId, CancellationToken ct)
    {
        var existing = await dbContext.PlayerSaleLocks
            .FirstOrDefaultAsync(l => l.LeagueId == leagueId && l.PlayerId == playerId, ct);

        bool locked;
        if (existing is null)
        {
            dbContext.PlayerSaleLocks.Add(new PlayerSaleLock { LeagueId = leagueId, PlayerId = playerId });
            locked = true;
        }
        else
        {
            dbContext.PlayerSaleLocks.Remove(existing);
            locked = false;
        }

        await dbContext.SaveChangesAsync(ct);
        return Ok(new { locked });
    }
}
