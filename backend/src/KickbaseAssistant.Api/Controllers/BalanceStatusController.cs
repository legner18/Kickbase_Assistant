using KickbaseAssistant.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api/leagues/{leagueId}/balance-status")]
public class BalanceStatusController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> Get(string leagueId, CancellationToken ct)
    {
        var status = await dbContext.LeagueBalanceStatuses
            .Where(s => s.LeagueId == leagueId)
            .Select(s => new
            {
                balance = s.Balance,
                creditLimit = s.CreditLimit,
                status = s.Status.ToString(),
                nextMatchdayNumber = s.NextMatchdayNumber,
                nextKickoffAt = s.NextKickoffAt,
                rosterGaps = s.RosterGaps,
                updatedAt = s.UpdatedAt,
            })
            .FirstOrDefaultAsync(ct);

        if (status is null)
        {
            return NotFound();
        }

        return Ok(status);
    }
}
