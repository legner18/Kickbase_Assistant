using KickbaseAssistant.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaguesController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetAll(CancellationToken ct)
    {
        var leagues = await dbContext.Leagues
            .Where(l => l.IsActive)
            .OrderBy(l => l.Name)
            .Select(l => new { id = l.Id, name = l.Name, competitionId = l.CompetitionId })
            .ToListAsync(ct);

        return Ok(leagues);
    }
}
