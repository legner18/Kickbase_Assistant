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
                price = r.Price,
                offerCount = r.OfferCount,
                expiresAt = r.ExpiresAt,
                momentumScore = r.MomentumScore,
                lineupScore = r.LineupScore,
                fixtureScore = r.FixtureScore,
                overallScore = r.OverallScore,
                scannedAt = r.ScannedAt,
            })
            .ToListAsync(ct);

        return Ok(results);
    }
}
