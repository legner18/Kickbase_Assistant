using KickbaseAssistant.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api/sell-alerts")]
public class SellAlertsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetAll([FromQuery] bool includeAcknowledged, CancellationToken ct)
    {
        var query = dbContext.SellAlerts.Include(a => a.Player).AsQueryable();
        if (!includeAcknowledged)
        {
            query = query.Where(a => !a.Acknowledged);
        }

        var alerts = await query
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                id = a.Id,
                playerId = a.PlayerId,
                playerName = a.Player!.Name,
                signal = a.Signal,
                reason = a.Reason,
                createdAt = a.CreatedAt,
                acknowledged = a.Acknowledged,
            })
            .ToListAsync(ct);

        return Ok(alerts);
    }

    [HttpPost("{id:long}/acknowledge")]
    public async Task<ActionResult> Acknowledge(long id, CancellationToken ct)
    {
        var alert = await dbContext.SellAlerts.FindAsync([id], ct);
        if (alert is null)
        {
            return NotFound();
        }

        alert.Acknowledged = true;
        await dbContext.SaveChangesAsync(ct);
        return NoContent();
    }
}
