using KickbaseAssistant.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api/leagues/{leagueId}")]
public class LeagueAnalysisController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet("manager-stats")]
    public async Task<ActionResult> GetManagerStats(string leagueId, CancellationToken ct)
    {
        var stats = await dbContext.LeagueManagerStats
            .Where(s => s.LeagueId == leagueId)
            .OrderByDescending(s => s.EstimatedBudget)
            .ToListAsync(ct);

        return Ok(stats);
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
