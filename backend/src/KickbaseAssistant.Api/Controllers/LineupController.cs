using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Players;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api/leagues/{leagueId}/lineup-optimizer")]
public class LineupController(AppDbContext dbContext, PlayerMetricsService playerMetricsService) : ControllerBase
{
    /// <summary>LigaInsider-Status in Startwahrscheinlichkeit - gleiche Stufen wie
    /// TransferMarketScanJob.ComputeLineupScoreAsync (dort 0-100, hier als Anteil 0-1).</summary>
    private static double StartProbability(string? lineupStatus) => lineupStatus switch
    {
        "Starting" => 1.0,
        "Doubtful" => 0.5,
        "Bench" => 0.2,
        "Out" => 0.0,
        _ => 0.5, // nie gescrapt - neutral
    };

    [HttpGet]
    public async Task<ActionResult> Get(string leagueId, CancellationToken ct)
    {
        var league = await dbContext.Leagues.FindAsync([leagueId], ct);
        if (league is null)
        {
            return NotFound();
        }

        var squadPlayers = await dbContext.SquadMemberships
            .Where(m => m.LeagueId == leagueId && m.ManagerId == league.OwnUserId)
            .Select(m => m.Player!)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Position,
                p.AveragePoints,
                p.StatusCode,
                LatestLineupStatus = dbContext.LineupPredictions
                    .Where(l => l.PlayerId == p.Id)
                    .OrderByDescending(l => l.ScrapedAt)
                    .Select(l => (string?)l.Status.ToString())
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        var candidates = new List<LineupCandidate>();
        foreach (var player in squadPlayers)
        {
            var expectedPoints = await playerMetricsService.ComputeExpectedPointsAsync(player.Id, player.AveragePoints, ct);
            candidates.Add(new LineupCandidate(
                player.Id, player.Name, player.Position, expectedPoints, StartProbability(player.LatestLineupStatus), player.StatusCode != 0));
        }

        var best = LineupOptimizer.FindBestLineup(candidates);
        if (best is null)
        {
            var positionLabels = new Dictionary<int, string> { [1] = "Torwart", [2] = "Abwehr", [3] = "Mittelfeld", [4] = "Sturm" };
            var (closestFormation, missing) = LineupOptimizer.FindClosestFormation(candidates);

            return Ok(new
            {
                formation = (string?)null,
                players = Array.Empty<object>(),
                totalExpectedPoints = 0,
                unfillablePositions = LineupOptimizer.FindUnfillablePositions(candidates),
                closestFormation = closestFormation.ToString(),
                missingPlayers = missing.Select(kv => new { position = positionLabels[kv.Key], count = kv.Value }),
            });
        }

        var selected = best.Value.PlayerIds
            .Select(id => candidates.First(c => c.PlayerId == id))
            .OrderByDescending(c => c.Position == 1) // TW zuerst, dann Reihenfolge der Auswahl
            .ToList();

        return Ok(new
        {
            formation = best.Value.Formation.ToString(),
            players = selected.Select(c => new
            {
                playerId = c.PlayerId,
                playerName = c.PlayerName,
                position = c.Position,
                expectedPoints = c.ExpectedPoints,
                startProbability = c.StartProbability,
                weightedPoints = c.WeightedPoints,
                injured = c.Injured,
            }),
            totalExpectedPoints = best.Value.TotalExpectedPoints,
            unfillablePositions = Array.Empty<string>(),
        });
    }
}
