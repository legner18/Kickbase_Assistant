using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

public record SetMarkingRequest(MarkingColor Color, string[] Labels, LineupEstimate? OwnLineupEstimate, DateTimeOffset? ExpiresAt);

[ApiController]
[Route("api/leagues/{leagueId}/markings")]
public class MarkingsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetAll(string leagueId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var markings = await dbContext.PlayerMarkings
            .Where(m => m.LeagueId == leagueId && (m.ExpiresAt == null || m.ExpiresAt > now))
            .Select(m => new
            {
                playerId = m.PlayerId,
                color = m.Color.ToString(),
                labels = m.Labels,
                ownLineupEstimate = m.OwnLineupEstimate == null ? null : m.OwnLineupEstimate.ToString(),
                expiresAt = m.ExpiresAt,
            })
            .ToListAsync(ct);

        return Ok(markings);
    }

    [HttpPut("{playerId}")]
    public async Task<ActionResult> Set(string leagueId, string playerId, [FromBody] SetMarkingRequest request, CancellationToken ct)
    {
        if (request.Labels.Length > 3)
        {
            return BadRequest("Maximal 3 Labels pro Spieler.");
        }

        var marking = await dbContext.PlayerMarkings.FirstOrDefaultAsync(m => m.LeagueId == leagueId && m.PlayerId == playerId, ct);
        if (marking is null)
        {
            marking = new PlayerMarking { LeagueId = leagueId, PlayerId = playerId, CreatedAt = DateTimeOffset.UtcNow };
            dbContext.PlayerMarkings.Add(marking);
        }

        marking.Color = request.Color;
        marking.Labels = request.Labels;
        marking.OwnLineupEstimate = request.OwnLineupEstimate;
        marking.ExpiresAt = request.ExpiresAt;

        await dbContext.SaveChangesAsync(ct);
        return Ok();
    }

    [HttpDelete("{playerId}")]
    public async Task<ActionResult> Delete(string leagueId, string playerId, CancellationToken ct)
    {
        var marking = await dbContext.PlayerMarkings.FirstOrDefaultAsync(m => m.LeagueId == leagueId && m.PlayerId == playerId, ct);
        if (marking is not null)
        {
            dbContext.PlayerMarkings.Remove(marking);
            await dbContext.SaveChangesAsync(ct);
        }

        return Ok();
    }
}
