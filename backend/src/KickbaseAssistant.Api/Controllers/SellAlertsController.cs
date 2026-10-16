using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Leagues;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api/sell-alerts")]
public class SellAlertsController(AppDbContext dbContext) : ControllerBase
{
    /// <summary>
    /// Nur Alarme für Spieler, die aktuell im eigenen Kader stehen - in der übergebenen Liga bzw.
    /// ohne leagueId in allen aktiven Ligen. SellAlert selbst hat keine Liga, alte Alarme für
    /// verkaufte Spieler oder archivierte Ligen bleiben in der DB, werden hier aber ausgeblendet.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> GetAll([FromQuery] bool includeAcknowledged, [FromQuery] string? leagueId, CancellationToken ct)
    {
        var ownPlayerIds = dbContext.OwnSquadPlayerIds(leagueId);
        var query = dbContext.SellAlerts.Include(a => a.Player).Where(a => ownPlayerIds.Contains(a.PlayerId));
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
