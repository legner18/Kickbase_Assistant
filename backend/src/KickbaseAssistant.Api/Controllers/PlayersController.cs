using KickbaseAssistant.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api")]
public class PlayersController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet("leagues/{leagueId}/squad")]
    public async Task<ActionResult> GetSquad(string leagueId, [FromQuery] string? managerId, CancellationToken ct)
    {
        var effectiveManagerId = managerId;
        if (effectiveManagerId is null)
        {
            var league = await dbContext.Leagues.FindAsync([leagueId], ct);
            if (league is null)
            {
                return NotFound();
            }
            effectiveManagerId = league.OwnUserId;
        }

        var players = await dbContext.SquadMemberships
            .Where(m => m.LeagueId == leagueId && m.ManagerId == effectiveManagerId)
            .Select(m => m.Player!)
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
                averagePoints = p.AveragePoints,
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
                sellScore = dbContext.SquadPlayerScores
                    .Where(r => r.LeagueId == leagueId && r.PlayerId == p.Id)
                    .Select(r => (double?)r.OverallScore)
                    .FirstOrDefault(),
                sellMomentumScore = dbContext.SquadPlayerScores
                    .Where(r => r.LeagueId == leagueId && r.PlayerId == p.Id)
                    .Select(r => (double?)r.MomentumScore)
                    .FirstOrDefault(),
                sellLineupScore = dbContext.SquadPlayerScores
                    .Where(r => r.LeagueId == leagueId && r.PlayerId == p.Id)
                    .Select(r => (double?)r.LineupScore)
                    .FirstOrDefault(),
                sellFixtureScore = dbContext.SquadPlayerScores
                    .Where(r => r.LeagueId == leagueId && r.PlayerId == p.Id)
                    .Select(r => (double?)r.FixtureScore)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return Ok(players);
    }

    [HttpGet("players/search")]
    public async Task<ActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
        {
            return Ok(Array.Empty<object>());
        }

        var term = q.Trim();
        var results = await dbContext.Players
            .Where(p => EF.Functions.ILike(p.Name, $"%{term}%"))
            .OrderByDescending(p => EF.Functions.ILike(p.Name, $"{term}%"))
            .ThenBy(p => p.Name)
            .Take(20)
            .Select(p => new
            {
                id = p.Id,
                name = p.Name,
                teamName = p.TeamName,
                position = p.Position,
                marketValue = p.MarketValue,
                totalPoints = p.TotalPoints,
            })
            .ToListAsync(ct);

        return Ok(results);
    }

    [HttpGet("players/value-ranking")]
    public async Task<ActionResult> GetValueRanking([FromQuery] int? position, CancellationToken ct)
    {
        // Preis pro Punkt = Marktwert / Gesamtpunkte - niedriger ist besser ("mehr Punkte fürs Geld").
        // Ein Mindestpunktestand (20) filtert Spieler mit kaum Einsätzen raus, bei denen ein einzelner
        // Punkt die Quote bedeutungslos verzerren würde.
        var query = dbContext.Players.Where(p => p.TotalPoints >= 20);
        if (position is > 0)
        {
            query = query.Where(p => p.Position == position);
        }

        var ranking = await query
            .OrderBy(p => (double)p.MarketValue / p.TotalPoints)
            .Take(100)
            .Select(p => new
            {
                id = p.Id,
                name = p.Name,
                teamName = p.TeamName,
                position = p.Position,
                marketValue = p.MarketValue,
                totalPoints = p.TotalPoints,
                averagePoints = p.AveragePoints,
                pricePerPoint = (double)p.MarketValue / p.TotalPoints,
            })
            .ToListAsync(ct);

        return Ok(ranking);
    }

    [HttpGet("players/{id}/comparables")]
    public async Task<ActionResult> GetComparables(string id, CancellationToken ct)
    {
        var player = await dbContext.Players.FindAsync([id], ct);
        if (player is null)
        {
            return NotFound();
        }

        var samePosition = dbContext.Players.Where(p => p.Position == player.Position && p.Id != id);

        var similarByValue = await samePosition
            .OrderBy(p => Math.Abs(p.MarketValue - player.MarketValue))
            .Take(5)
            .Select(p => new { id = p.Id, name = p.Name, teamName = p.TeamName, marketValue = p.MarketValue, totalPoints = p.TotalPoints, averagePoints = p.AveragePoints })
            .ToListAsync(ct);

        var similarByPoints = await samePosition
            .OrderBy(p => Math.Abs(p.TotalPoints - player.TotalPoints))
            .Take(5)
            .Select(p => new { id = p.Id, name = p.Name, teamName = p.TeamName, marketValue = p.MarketValue, totalPoints = p.TotalPoints, averagePoints = p.AveragePoints })
            .ToListAsync(ct);

        return Ok(new { similarByValue, similarByPoints });
    }

    [HttpGet("players/{id}")]
    public async Task<ActionResult> GetDetail(string id, CancellationToken ct)
    {
        var player = await dbContext.Players.FindAsync([id], ct);
        if (player is null)
        {
            return NotFound();
        }

        var lineupHistory = await dbContext.LineupPredictions
            .Where(l => l.PlayerId == id)
            .OrderByDescending(l => l.ScrapedAt)
            .Take(30)
            .Select(l => new { status = l.Status.ToString(), scrapedAt = l.ScrapedAt, source = l.Source })
            .ToListAsync(ct);

        var sellAlerts = await dbContext.SellAlerts
            .Where(a => a.PlayerId == id)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new { signal = a.Signal.ToString(), reason = a.Reason, createdAt = a.CreatedAt, acknowledged = a.Acknowledged })
            .ToListAsync(ct);

        var owners = await dbContext.SquadMemberships
            .Where(m => m.PlayerId == id)
            .Select(m => new { m.LeagueId, m.ManagerId, m.ManagerName })
            .ToListAsync(ct);

        return Ok(new
        {
            id = player.Id,
            name = player.Name,
            teamId = player.TeamId,
            teamName = player.TeamName,
            position = player.Position,
            marketValue = player.MarketValue,
            totalPoints = player.TotalPoints,
            averagePoints = player.AveragePoints,
            lastSeenAt = player.LastSeenAt,
            lineupHistory,
            sellAlerts,
            owners,
        });
    }

    [HttpGet("players/{id}/market-value-history")]
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
