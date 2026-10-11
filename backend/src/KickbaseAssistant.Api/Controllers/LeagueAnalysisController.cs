using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Players;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api/leagues/{leagueId}")]
public class LeagueAnalysisController(AppDbContext dbContext, PlayerMetricsService playerMetricsService) : ControllerBase
{
    [HttpGet("manager-stats")]
    public async Task<ActionResult> GetManagerStats(string leagueId, CancellationToken ct)
    {
        var stats = await dbContext.LeagueManagerStats
            .Where(s => s.LeagueId == leagueId)
            .OrderByDescending(s => s.EstimatedBudget)
            .ToListAsync(ct);

        // Umbauplan "Transfer-Bilanz je Manager": Verkaufspreis - Kaufpreis, als laufende
        // Handelsbilanz statt exakter Buy/Sell-Paarung pro Spieler (die Reihenfolge je Transfer
        // ist durch die History nicht immer eindeutig rekonstruierbar, der Netto-Cashflow schon).
        var transferBalances = await dbContext.LeagueTransfers
            .Where(t => t.LeagueId == leagueId)
            .GroupBy(t => t.ManagerId)
            .Select(g => new
            {
                ManagerId = g.Key,
                Balance = g.Sum(t => t.Type == TransferType.Sell ? t.Price : -t.Price),
            })
            .ToDictionaryAsync(x => x.ManagerId, x => x.Balance, ct);

        return Ok(stats.Select(s => new
        {
            s.Id,
            s.LeagueId,
            s.ManagerId,
            s.ManagerName,
            s.EstimatedBudget,
            s.AverageOverpayPercent,
            s.TransferCount,
            s.SeasonPoints,
            s.MatchdayPoints,
            s.SeasonRank,
            s.MatchdayRank,
            s.CurrentMatchday,
            s.UpdatedAt,
            transferBalance = transferBalances.GetValueOrDefault(s.ManagerId, 0),
        }));
    }

    /// <summary>Umbauplan "Kader der Mitmanager im Fair-Value-Vergleich": wer hat über-, wer
    /// unterbewertete Spieler. Nutzt dieselbe Fair-Value-Regression wie die Preis-Leistung-Seite
    /// (Phase 1), hier pro Manager aufsummiert statt pro Spieler.</summary>
    [HttpGet("fair-value-comparison")]
    public async Task<ActionResult> GetFairValueComparison(string leagueId, CancellationToken ct)
    {
        var fairValues = await playerMetricsService.ComputeFairValuesAsync(ct);

        var squadsByManager = await dbContext.SquadMemberships
            .Where(m => m.LeagueId == leagueId)
            .Select(m => new { m.ManagerId, m.ManagerName, m.PlayerId, Player = m.Player! })
            .ToListAsync(ct);

        var comparison = squadsByManager
            .GroupBy(m => new { m.ManagerId, m.ManagerName })
            .Select(g =>
            {
                var marketValue = g.Sum(m => m.Player.MarketValue);
                var fairValue = g.Sum(m => fairValues.GetValueOrDefault(m.PlayerId, m.Player.MarketValue));
                return new
                {
                    managerId = g.Key.ManagerId,
                    managerName = g.Key.ManagerName,
                    playerCount = g.Count(),
                    squadMarketValue = marketValue,
                    squadFairValue = fairValue,
                    deltaPercent = marketValue > 0 ? (fairValue - marketValue) / marketValue * 100 : 0,
                };
            })
            .OrderByDescending(x => x.deltaPercent)
            .ToList();

        return Ok(comparison);
    }

    [HttpGet("transfers")]
    public async Task<ActionResult> GetTransfers(string leagueId, CancellationToken ct)
    {
        var transfers = await dbContext.LeagueTransfers
            .Where(t => t.LeagueId == leagueId)
            .OrderByDescending(t => t.OccurredAt)
            .Take(200)
            .ToListAsync(ct);

        return Ok(transfers);
    }
}
