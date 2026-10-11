namespace KickbaseAssistant.Domain.Players;

/// <summary>
/// Spielerbewertung (Umbauplan "Fair Value"): einfache lineare Kleinste-Quadrate-Regression
/// Marktwert ~ Ø Punkte über alle Spieler mit genug Einsätzen. Bewusst reine Mathematik statt ML
/// (LightGBM & Co. sind laut Plan erst Phase 3, wenn eine echte Punkteprognose ansteht).
/// </summary>
public static class FairValueCalculator
{
    public static (double Slope, double Intercept) FitLinearRegression(IReadOnlyList<(double X, double Y)> points)
    {
        if (points.Count == 0)
        {
            return (0, 0);
        }

        if (points.Count == 1)
        {
            return (0, points[0].Y);
        }

        var n = points.Count;
        var sumX = points.Sum(p => p.X);
        var sumY = points.Sum(p => p.Y);
        var sumXY = points.Sum(p => p.X * p.Y);
        var sumXX = points.Sum(p => p.X * p.X);

        var denominator = n * sumXX - sumX * sumX;
        if (denominator == 0)
        {
            // Alle X-Werte identisch (z.B. nur Spieler mit derselben Ø-Punktzahl) - keine Steigung
            // bestimmbar, Fair Value fällt auf den Mittelwert zurück.
            return (0, sumY / n);
        }

        var slope = (n * sumXY - sumX * sumY) / denominator;
        var intercept = (sumY - slope * sumX) / n;
        return (slope, intercept);
    }

    public static double Predict(double slope, double intercept, double x) => slope * x + intercept;
}
