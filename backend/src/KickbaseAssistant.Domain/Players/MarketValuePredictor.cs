namespace KickbaseAssistant.Domain.Players;

/// <summary>
/// Umbauplan "MW-Prognose 22 Uhr": "eigenes Modell aus MW-Historie und Trend" - bewusst ein
/// einfacher gewichteter Durchschnitt (neuere Tage zählen mehr), keine echte ML-Prognose. Gleiche
/// "kein KI nötig, wo einfache Mathematik reicht"-Linie wie FairValueCalculator und
/// MarketValueTrendAnalyzer.
/// </summary>
public static class MarketValuePredictor
{
    /// <summary>percentChanges: älteste zuerst. Leere Liste -> 0 (keine Aussage möglich).</summary>
    public static double PredictNextChangePercent(IReadOnlyList<double> percentChanges)
    {
        if (percentChanges.Count == 0)
        {
            return 0;
        }

        double weightedSum = 0;
        double weightTotal = 0;
        for (var i = 0; i < percentChanges.Count; i++)
        {
            var weight = i + 1; // je neuer, desto höher gewichtet
            weightedSum += percentChanges[i] * weight;
            weightTotal += weight;
        }

        return weightedSum / weightTotal;
    }

    public static double PredictNextValue(double currentValue, double predictedChangePercent) =>
        currentValue * (1 + predictedChangePercent / 100.0);

    public const int DailyUpdateHour = 22;

    public static double PredictChangeOverUpdates(double currentValue, double predictedChangePercent, int updateCount) =>
        updateCount <= 0 ? 0 : currentValue * (Math.Pow(1 + predictedChangePercent / 100.0, updateCount) - 1);

    public static int CountUpdatesUntil(DateTimeOffset from, DateTimeOffset until, TimeZoneInfo timeZone)
    {
        if (until <= from)
        {
            return 0;
        }

        var localFrom = TimeZoneInfo.ConvertTime(from, timeZone);
        var localUntil = TimeZoneInfo.ConvertTime(until, timeZone);
        var count = 0;

        for (var day = localFrom.Date; day <= localUntil.Date; day = day.AddDays(1))
        {
            var updateTime = day.AddHours(DailyUpdateHour);
            if (updateTime > localFrom.DateTime && updateTime < localUntil.DateTime)
            {
                count++;
            }
        }

        return count;
    }
}
