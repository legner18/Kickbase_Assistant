using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlayersController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Player>>> GetAll(CancellationToken ct)
    {
        var players = await dbContext.Players
            .Where(p => p.IsInSquad)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        return Ok(players);
    }
}
