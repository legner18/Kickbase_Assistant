using KickbaseAssistant.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlayersController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetAll(CancellationToken ct)
    {
        var players = await dbContext.Players
            .Where(p => p.IsInSquad)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                id = p.Id,
                name = p.Name,
                teamId = p.TeamId,
                teamName = p.TeamName,
                position = p.Position,
                marketValue = p.MarketValue,
                totalPoints = p.TotalPoints,
                lastSeenAt = p.LastSeenAt,
                latestLineupStatus = dbContext.LineupPredictions
                    .Where(l => l.PlayerId == p.Id)
                    .OrderByDescending(l => l.ScrapedAt)
                    .Select(l => (string?)l.Status.ToString())
                    .FirstOrDefault(),
                latestMarketValueChange = dbContext.MarketValueSnapshots
                    .Where(s => s.PlayerId == p.Id)
                    .OrderByDescending(s => s.Date)
                    .Select(s => (int?)s.MarketValueChange)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return Ok(players);
    }

    [HttpGet("{id}/market-value-history")]
    public async Task<ActionResult> GetMarketValueHistory(string id, CancellationToken ct)
    {
        var history = await dbContext.MarketValueSnapshots
            .Where(s => s.PlayerId == id)
            .OrderBy(s => s.Date)
            .Select(s => new { date = s.Date, marketValue = s.MarketValue, change = s.MarketValueChange })
            .ToListAsync(ct);

        return Ok(history);
    }
}
