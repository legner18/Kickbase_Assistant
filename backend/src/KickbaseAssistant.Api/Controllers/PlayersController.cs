using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Players;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Api.Controllers;

[ApiController]
[Route("api")]
public class PlayersController(AppDbContext dbContext, PlayerMetricsService playerMetricsService) : ControllerBase
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
                statusCode = p.StatusCode,
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
                // Umbauplan: Kaufpreis darf als Info angezeigt werden, darf aber nirgends in eine
                // Entscheidung (Sortierung, Verkaufsplaner-Ziel) einfließen - reiner Anzeigewert.
                // Letzter bekannter Kauf dieses Managers für diesen Spieler; null, wenn der Spieler
                // vor Beginn des Transfer-Trackings da war oder zugelost wurde (kein Buy-Eintrag).
                purchasePrice = dbContext.LeagueTransfers
                    .Where(t => t.LeagueId == leagueId && t.ManagerId == effectiveManagerId && t.PlayerId == p.Id && t.Type == TransferType.Buy)
                    .OrderByDescending(t => t.OccurredAt)
                    .Select(t => (int?)t.Price)
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

        var ranked = await query
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

        var fairValues = await playerMetricsService.ComputeFairValuesAsync(ct);

        var ranking = ranked.Select(p =>
        {
            var fairValue = fairValues.GetValueOrDefault(p.id);
            return new
            {
                p.id,
                p.name,
                p.teamName,
                p.position,
                p.marketValue,
                p.totalPoints,
                p.averagePoints,
                p.pricePerPoint,
                pointsPerMillion = p.marketValue > 0 ? p.averagePoints / (p.marketValue / 1_000_000.0) : 0,
                fairValue,
                fairValueDeltaPercent = p.marketValue > 0 ? (fairValue - p.marketValue) / p.marketValue * 100 : 0,
            };
        });

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

        var upcomingFixtures = await dbContext.TeamFixtures
            .Where(f => f.CompetitionId == player.CompetitionId.ToString() && f.TeamId == player.TeamId)
            .OrderBy(f => f.MatchdayNumber)
            .Select(f => new
            {
                matchdayNumber = f.MatchdayNumber,
                opponentTeamId = f.OpponentTeamId,
                opponentTeamName = f.OpponentTeamName,
                isHome = f.IsHome,
                difficultyTier = f.DifficultyTier,
            })
            .ToListAsync(ct);

        var fairValues = await playerMetricsService.ComputeFairValuesAsync(ct);
        var fairValue = fairValues.GetValueOrDefault(player.Id);
        var matchdayMetrics = await playerMetricsService.ComputeMatchdayMetricsAsync(player.Id, ct);

        // Umbauplan "MW-Prognose 22 Uhr": gewichteter Trend der letzten 5 Tage, siehe
        // MarketValuePredictor - bewusst kein ML, nur ein einfaches, erklärbares Modell.
        var recentSnapshots = await dbContext.MarketValueSnapshots
            .Where(s => s.PlayerId == id)
            .OrderByDescending(s => s.Date)
            .Take(5)
            .Select(s => new { s.MarketValue, s.MarketValueChange })
            .ToListAsync(ct);
        var recentPercentChanges = recentSnapshots
            .AsEnumerable()
            .Reverse()
            .Select(s =>
            {
                var previousValue = s.MarketValue - s.MarketValueChange;
                return previousValue > 0 ? s.MarketValueChange / (double)previousValue * 100 : 0;
            })
            .ToList();
        var predictedChangePercent = MarketValuePredictor.PredictNextChangePercent(recentPercentChanges);
        var predictedNextValue = MarketValuePredictor.PredictNextValue(player.MarketValue, predictedChangePercent);

        var marketListingHistory = await dbContext.MarketListingHistories
            .Where(h => h.PlayerId == id)
            .OrderByDescending(h => h.ListedAt)
            .Take(10)
            .Select(h => new { h.LeagueId, h.ListedAt, h.RemovedAt, h.Price, outcome = h.Outcome.ToString() })
            .ToListAsync(ct);

        // Umbauplan "Besitzer-Verlauf je Spieler": Kette Kickbase -> ManagerA (4 Tage) -> ... - pro
        // Liga eigenständig, da Kader-Ökonomien zwischen Ligen unabhängig sind.
        var transferHistory = await dbContext.LeagueTransfers
            .Where(t => t.PlayerId == id)
            .OrderBy(t => t.OccurredAt)
            .Select(t => new { t.LeagueId, t.ManagerName, t.Type, t.Price, t.OccurredAt })
            .ToListAsync(ct);
        var leagueNames = await dbContext.Leagues
            .Where(l => transferHistory.Select(t => t.LeagueId).Distinct().Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Name, ct);
        var ownershipHistory = transferHistory
            .GroupBy(t => t.LeagueId)
            .Select(g =>
            {
                var chain = g.OrderBy(t => t.OccurredAt).ToList();
                var entries = chain.Select((t, i) =>
                {
                    DateTimeOffset? heldUntil = i + 1 < chain.Count ? chain[i + 1].OccurredAt : null;
                    return new
                    {
                        managerName = t.ManagerName,
                        type = t.Type.ToString(),
                        price = t.Price,
                        occurredAt = t.OccurredAt,
                        heldDays = heldUntil.HasValue ? (int?)(heldUntil.Value - t.OccurredAt).TotalDays : null,
                    };
                });
                return new { leagueId = g.Key, leagueName = leagueNames.GetValueOrDefault(g.Key, g.Key), chain = entries };
            })
            .ToList();

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
            statusCode = player.StatusCode,
            lastSeenAt = player.LastSeenAt,
            fairValue,
            fairValueDeltaPercent = player.MarketValue > 0 ? (fairValue - player.MarketValue) / player.MarketValue * 100 : 0,
            pointsPerMillion = player.MarketValue > 0 ? player.AveragePoints / (player.MarketValue / 1_000_000.0) : 0,
            medianPoints = matchdayMetrics?.MedianPoints,
            startingXiQuota = matchdayMetrics?.StartingXiQuota,
            pointsPerMinute = matchdayMetrics?.PointsPerMinute,
            lineupHistory,
            sellAlerts,
            owners,
            upcomingFixtures,
            marketListingHistory,
            ownershipHistory,
            predictedChangePercent,
            predictedNextValue,
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
