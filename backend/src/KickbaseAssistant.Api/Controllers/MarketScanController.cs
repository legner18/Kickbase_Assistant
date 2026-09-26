using KickbaseAssistant.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api/leagues/{leagueId}/market-scan")]
public class MarketScanController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetScan(string leagueId, CancellationToken ct)
    {
        var results = await dbContext.MarketScanResults
            .Where(r => r.LeagueId == leagueId)
            .Include(r => r.Player)
            .OrderByDescending(r => r.OverallScore)
            .Select(r => new
            {
                playerId = r.PlayerId,
                playerName = r.Player!.Name,
                teamId = r.Player.TeamId,
                teamName = r.Player.TeamName,
                position = r.Player.Position,
                price = r.Price,
                offerCount = r.OfferCount,
                expiresAt = r.ExpiresAt,
                momentumScore = r.MomentumScore,
                lineupScore = r.LineupScore,
                fixtureScore = r.FixtureScore,
                overallScore = r.OverallScore,
                marketValueChangeAbs = r.MarketValueChangeAbs,
                marketValueChangePercent = r.MarketValueChangePercent,
                // Ein Marktangebot ist ein Verkauf, aber der Spieler bleibt bis zum Zuschlag im
                // Kader des bisherigen Besitzers - genau das findet sich hier in SquadMemberships.
                ownerManagerName = dbContext.SquadMemberships
                    .Where(m => m.LeagueId == leagueId && m.PlayerId == r.PlayerId)
                    .Select(m => (string?)m.ManagerName)
                    .FirstOrDefault(),
                scannedAt = r.ScannedAt,
            })
            .ToListAsync(ct);

        return Ok(results);
    }
}
