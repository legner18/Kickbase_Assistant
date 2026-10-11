namespace KickbaseAssistant.Domain.Players;

public enum MarketValueTrendStatus
{
    /// <summary>Zu wenig Historie für eine Aussage.</summary>
    Unknown,
    Rising,
    /// <summary>3 Tage in Folge schwächere tägliche Steigerung, aber noch über der Verkaufs-Schwelle.</summary>
    PeakApproaching,
    /// <summary>Tägliche Änderung negativ oder unter der Schwelle - jetzt verkaufen.</summary>
    SellSignal,
}

/// <summary>
/// Umbauplan "Verkaufs-Timing und Trend-Alarm": erkennt, wenn ein steigender Marktwert anfängt
/// abzuflachen ("Peak naht"), statt nur auf einen bereits fallenden Wert zu reagieren (das deckt
/// bereits SellAlertJob.CheckMomentumAsync ab). Arbeitet auf täglichen prozentualen Änderungen,
/// nicht absoluten €-Beträgen, damit Spieler unterschiedlicher Preisklassen vergleichbar sind.
/// </summary>
public static class MarketValueTrendAnalyzer
{
    public const double SellThresholdPercent = 0.5;

    /// <summary>percentChanges: älteste zuerst, genau die letzten 3 Tage.</summary>
    public static MarketValueTrendStatus Analyze(IReadOnlyList<double> percentChanges)
    {
        if (percentChanges.Count < 3)
        {
            return MarketValueTrendStatus.Unknown;
        }

        var latest = percentChanges[^1];
        if (latest < SellThresholdPercent)
        {
            return MarketValueTrendStatus.SellSignal;
        }

        if (percentChanges[0] > percentChanges[1] && percentChanges[1] > percentChanges[2])
        {
            return MarketValueTrendStatus.PeakApproaching;
        }

        return MarketValueTrendStatus.Rising;
    }
}
