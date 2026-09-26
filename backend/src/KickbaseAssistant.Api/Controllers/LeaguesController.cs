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
            .Select(l => new { id = l.Id, name = l.Name, competitionId = l.CompetitionId, ownUserId = l.OwnUserId })
            .ToListAsync(ct);

        return Ok(leagues);
    }

    [HttpGet("{leagueId}/managers")]
    public async Task<ActionResult> GetManagers(string leagueId, CancellationToken ct)
    {
        var league = await dbContext.Leagues.FindAsync([leagueId], ct);
        if (league is null)
        {
            return NotFound();
        }

        var managers = await dbContext.SquadMemberships
            .Where(m => m.LeagueId == leagueId)
            .Select(m => new { m.ManagerId, m.ManagerName })
            .Distinct()
            .ToListAsync(ct);

        var result = managers
            .Select(m => new { managerId = m.ManagerId, managerName = m.ManagerName, isMe = m.ManagerId == league.OwnUserId })
            .OrderByDescending(m => m.isMe)
            .ThenBy(m => m.managerName);

        return Ok(result);
    }
}
