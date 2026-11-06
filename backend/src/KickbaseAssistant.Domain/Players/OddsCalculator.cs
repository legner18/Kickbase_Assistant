namespace KickbaseAssistant.Domain.Players;

public readonly record struct OutcomeOdds(double Home, double Draw, double Away);

public readonly record struct OutcomeProbabilities(double Home, double Draw, double Away);

public readonly record struct ExpectedGoals(double Home, double Away);

public static class OddsCalculator
{
    private const int MaximumGoals = 10;
    private const double CoarseStep = 0.05;
    private const double FineStep = 0.01;
    private const double MinimumGoals = 0.1;
    private const double MaximumExpectedGoals = 5.0;

    public static OutcomeProbabilities? ToProbabilities(OutcomeOdds odds)
    {
        if (odds.Home <= 1 || odds.Draw <= 1 || odds.Away <= 1)
        {
            return null;
        }

        var home = 1 / odds.Home;
        var draw = 1 / odds.Draw;
        var away = 1 / odds.Away;
        var total = home + draw + away;
        return new OutcomeProbabilities(home / total, draw / total, away / total);
    }

    public static OutcomeProbabilities Average(IReadOnlyCollection<OutcomeProbabilities> probabilities) =>
        new(
            probabilities.Average(probability => probability.Home),
            probabilities.Average(probability => probability.Draw),
            probabilities.Average(probability => probability.Away));

    public static OutcomeProbabilities PoissonOutcome(ExpectedGoals expectedGoals)
    {
        var homeDistribution = PoissonDistribution(expectedGoals.Home);
        var awayDistribution = PoissonDistribution(expectedGoals.Away);
        double home = 0;
        double draw = 0;
        double away = 0;

        for (var homeGoals = 0; homeGoals <= MaximumGoals; homeGoals++)
        {
            for (var awayGoals = 0; awayGoals <= MaximumGoals; awayGoals++)
            {
                var probability = homeDistribution[homeGoals] * awayDistribution[awayGoals];
                if (homeGoals > awayGoals)
                {
                    home += probability;
                }
                else if (homeGoals == awayGoals)
                {
                    draw += probability;
                }
                else
                {
                    away += probability;
                }
            }
        }

        var total = home + draw + away;
        return new OutcomeProbabilities(home / total, draw / total, away / total);
    }

    public static ExpectedGoals EstimateExpectedGoals(OutcomeProbabilities target)
    {
        var best = Search(target, MinimumGoals, MaximumExpectedGoals, MinimumGoals, MaximumExpectedGoals, CoarseStep);
        return Search(
            target,
            Math.Max(MinimumGoals, best.Home - CoarseStep),
            Math.Min(MaximumExpectedGoals, best.Home + CoarseStep),
            Math.Max(MinimumGoals, best.Away - CoarseStep),
            Math.Min(MaximumExpectedGoals, best.Away + CoarseStep),
            FineStep);
    }

    private static ExpectedGoals Search(
        OutcomeProbabilities target, double homeFrom, double homeTo, double awayFrom, double awayTo, double step)
    {
        var best = new ExpectedGoals(homeFrom, awayFrom);
        var bestError = double.MaxValue;

        for (var homeGoals = homeFrom; homeGoals <= homeTo + 1e-9; homeGoals += step)
        {
            for (var awayGoals = awayFrom; awayGoals <= awayTo + 1e-9; awayGoals += step)
            {
                var candidate = new ExpectedGoals(homeGoals, awayGoals);
                var outcome = PoissonOutcome(candidate);
                var error = Math.Pow(outcome.Home - target.Home, 2)
                    + Math.Pow(outcome.Draw - target.Draw, 2)
                    + Math.Pow(outcome.Away - target.Away, 2);
                if (error < bestError)
                {
                    bestError = error;
                    best = candidate;
                }
            }
        }

        return best;
    }

    private static double[] PoissonDistribution(double mean)
    {
        var distribution = new double[MaximumGoals + 1];
        distribution[0] = Math.Exp(-mean);
        for (var goals = 1; goals <= MaximumGoals; goals++)
        {
            distribution[goals] = distribution[goals - 1] * mean / goals;
        }

        return distribution;
    }
}
