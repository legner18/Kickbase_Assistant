using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Infrastructure.Players;

public record PlayerMatchdayMetrics(double? MedianPoints, double? StartingXiQuota, double? PointsPerMinute);

/// <summary>
/// Umbauplan "Spielerbewertung": Fair Value (Regression über alle Spieler mit genug Einsätzen,
/// siehe FairValueCalculator) sowie Median/S11-Quote/Pkt-Min (aus PlayerMatchdayPerformance, nur
/// die laufende Saison - ältere Saisons sind für "ist der aktuelle Marktwert fair" nicht relevant).
/// Reine Lesezugriffe auf bereits von den Worker-Jobs befüllte Daten, kein Kickbase-Aufruf.
/// </summary>
public class PlayerMetricsService(AppDbContext dbContext)
{
    private const int MinTotalPointsForFairValue = 20;
    private const int MinMatchdaysForMedianAndPerMinute = 5;

    /// <summary>Fair Value je Spieler-ID, nur für Spieler, die die Mindest-Einsatzschwelle erfüllen.</summary>
    public async Task<IReadOnlyDictionary<string, double>> ComputeFairValuesAsync(CancellationToken ct)
    {
        var players = await dbContext.Players
            .Where(p => p.TotalPoints >= MinTotalPointsForFairValue)
            .Select(p => new { p.Id, p.AveragePoints, p.MarketValue })
            .ToListAsync(ct);

        var (slope, intercept) = FairValueCalculator.FitLinearRegression(
            [.. players.Select(p => ((double)p.AveragePoints, (double)p.MarketValue))]);

        return players.ToDictionary(p => p.Id, p => FairValueCalculator.Predict(slope, intercept, p.AveragePoints));
    }

    /// <summary>
    /// Umbauplan "Verkaufsplaner": Ø Punkte der letzten (bis zu) 5 gespielten Spieltage der
    /// laufenden Saison - "bis die Prognose steht" laut Plan. Fällt auf den übergebenen
    /// Saison-Durchschnitt zurück, wenn (noch) keine Spieltags-Daten vorliegen.
    /// </summary>
    public async Task<double> ComputeExpectedPointsAsync(string playerId, double fallbackAveragePoints, CancellationToken ct)
    {
        var currentSeasonId = await ResolveCurrentSeasonIdAsync(playerId, ct);
        if (currentSeasonId is null)
        {
            return fallbackAveragePoints;
        }

        var recentPoints = await dbContext.PlayerMatchdayPerformances
            .Where(p => p.PlayerId == playerId && p.SeasonId == currentSeasonId && p.MinutesPlayed > 0)
            .OrderByDescending(p => p.MatchdayNumber)
            .Take(5)
            .Select(p => p.Points)
            .ToListAsync(ct);

        return recentPoints.Count > 0 ? recentPoints.Average() : fallbackAveragePoints;
    }

    public async Task<PlayerMatchdayMetrics?> ComputeMatchdayMetricsAsync(string playerId, CancellationToken ct)
    {
        var currentSeasonId = await ResolveCurrentSeasonIdAsync(playerId, ct);
        if (currentSeasonId is null)
        {
            return null;
        }

        var matchdays = await dbContext.PlayerMatchdayPerformances
            .Where(p => p.PlayerId == playerId && p.SeasonId == currentSeasonId)
            .Select(p => new { p.Points, p.MinutesPlayed, p.StartedInLineup })
            .ToListAsync(ct);

        var startingXiQuota = matchdays.Count > 0
            ? matchdays.Count(m => m.StartedInLineup) / (double)matchdays.Count
            : (double?)null;

        var played = matchdays.Where(m => m.MinutesPlayed > 0).ToList();
        double? median = null;
        double? pointsPerMinute = null;

        if (played.Count >= MinMatchdaysForMedianAndPerMinute)
        {
            median = Median([.. played.Select(m => (double)m.Points)]);
            var totalMinutes = played.Sum(m => m.MinutesPlayed);
            pointsPerMinute = totalMinutes > 0 ? played.Sum(m => m.Points) / (double)totalMinutes : null;
        }

        return new PlayerMatchdayMetrics(median, startingXiQuota, pointsPerMinute);
    }

    private async Task<string?> ResolveCurrentSeasonIdAsync(string playerId, CancellationToken ct)
    {
        var seasonIds = await dbContext.PlayerMatchdayPerformances
            .Where(p => p.PlayerId == playerId)
            .Select(p => p.SeasonId)
            .Distinct()
            .ToListAsync(ct);

        return seasonIds.Count == 0
            ? null
            : seasonIds.OrderByDescending(s => int.TryParse(s, out var value) ? value : 0).First();
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        var mid = values.Count / 2;
        return values.Count % 2 == 0 ? (values[mid - 1] + values[mid]) / 2.0 : values[mid];
    }
}
