using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Infrastructure.Players;

public record PlayerMatchdayMetrics(double? MedianPoints, double? StartingXiQuota, double? PointsPerMinute);

public record FairValueModel(double Slope, double Intercept, IReadOnlyDictionary<string, double> FairValues);

public record PlayerMarketValueForecast(
    string PlayerId,
    int? LatestChange,
    double PredictedChangePercent,
    double PredictedNextChange,
    double? PredictedChangeUntilKickoff);

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
    private const int PredictionSnapshotCount = 5;

    public async Task<double> ComputePredictedChangePercentAsync(string playerId, CancellationToken ct)
    {
        var forecast = await ComputeForecastAsync(playerId, 0, updatesUntilKickoff: null, ct);
        return forecast.PredictedChangePercent;
    }

    public async Task<IReadOnlyList<PlayerMarketValueForecast>> ComputeForecastsAsync(
        IEnumerable<(string PlayerId, int MarketValue)> players, int? updatesUntilKickoff, CancellationToken ct)
    {
        var forecasts = new List<PlayerMarketValueForecast>();
        foreach (var (playerId, marketValue) in players)
        {
            forecasts.Add(await ComputeForecastAsync(playerId, marketValue, updatesUntilKickoff, ct));
        }

        return forecasts;
    }

    private async Task<PlayerMarketValueForecast> ComputeForecastAsync(
        string playerId, int marketValue, int? updatesUntilKickoff, CancellationToken ct)
    {
        var recentSnapshots = await dbContext.MarketValueSnapshots
            .Where(snapshot => snapshot.PlayerId == playerId)
            .OrderByDescending(snapshot => snapshot.Date)
            .Take(PredictionSnapshotCount)
            .Select(snapshot => new { snapshot.MarketValue, snapshot.MarketValueChange })
            .ToListAsync(ct);

        var percentChanges = recentSnapshots
            .AsEnumerable()
            .Reverse()
            .Select(snapshot =>
            {
                var previousValue = snapshot.MarketValue - snapshot.MarketValueChange;
                return previousValue > 0 ? snapshot.MarketValueChange / (double)previousValue * 100 : 0;
            })
            .ToList();

        var predictedChangePercent = MarketValuePredictor.PredictNextChangePercent(percentChanges);

        return new PlayerMarketValueForecast(
            playerId,
            recentSnapshots.Count > 0 ? recentSnapshots[0].MarketValueChange : null,
            predictedChangePercent,
            MarketValuePredictor.PredictChangeOverUpdates(marketValue, predictedChangePercent, 1),
            updatesUntilKickoff is { } updateCount
                ? MarketValuePredictor.PredictChangeOverUpdates(marketValue, predictedChangePercent, updateCount)
                : null);
    }

    /// <summary>Fair Value je Spieler-ID, nur für Spieler, die die Mindest-Einsatzschwelle erfüllen.</summary>
    public async Task<IReadOnlyDictionary<string, double>> ComputeFairValuesAsync(CancellationToken ct) =>
        (await ComputeFairValueModelAsync(ct)).FairValues;

    public async Task<FairValueModel> ComputeFairValueModelAsync(CancellationToken ct)
    {
        var players = await dbContext.Players
            .Where(player => player.TotalPoints >= MinTotalPointsForFairValue)
            .Select(player => new { player.Id, player.AveragePoints, player.MarketValue })
            .ToListAsync(ct);

        var (slope, intercept) = FairValueCalculator.FitLinearRegression(
            [.. players.Select(player => ((double)player.AveragePoints, (double)player.MarketValue))]);

        return new FairValueModel(
            slope,
            intercept,
            players.ToDictionary(player => player.Id, player => FairValueCalculator.Predict(slope, intercept, player.AveragePoints)));
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

    /// <summary>
    /// Kontostand-Wächter: Summe der positiven letzten Marktwert-Änderungen der übergebenen
    /// Spieler - der "erwartete Puffer" für BalanceGuardCalculator.GetStatus.
    /// </summary>
    public async Task<double> ComputePositiveMarketValueBufferAsync(IEnumerable<string> playerIds, CancellationToken ct)
    {
        var positiveBuffer = 0.0;
        foreach (var playerId in playerIds)
        {
            var latest = await dbContext.MarketValueSnapshots
                .Where(s => s.PlayerId == playerId)
                .OrderByDescending(s => s.Date)
                .FirstOrDefaultAsync(ct);
            if (latest is not null && latest.MarketValueChange > 0)
            {
                positiveBuffer += latest.MarketValueChange;
            }
        }

        return positiveBuffer;
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
