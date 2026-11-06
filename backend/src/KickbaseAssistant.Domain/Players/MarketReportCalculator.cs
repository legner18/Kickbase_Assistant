namespace KickbaseAssistant.Domain.Players;

public readonly record struct MarketValuePoint(string PlayerId, DateOnly Date, int MarketValue);

public readonly record struct DailyMarketMovement(
    DateOnly Date, long TotalChange, int RisingCount, int FallingCount, int UnchangedCount)
{
    public int PlayerCount => RisingCount + FallingCount + UnchangedCount;
}

public readonly record struct PlayerValueChange(string PlayerId, int MarketValue, int Change);

public static class MarketReportCalculator
{
    public const double MinimumCoverageShare = 0.5;

    public static IReadOnlyList<DailyMarketMovement> ComputeDailyMovements(IReadOnlyList<MarketValuePoint> points)
    {
        var valuesByDate = points
            .GroupBy(point => point.Date)
            .ToDictionary(
                dateGroup => dateGroup.Key,
                dateGroup => dateGroup.ToDictionary(point => point.PlayerId, point => point.MarketValue));

        var movements = new List<DailyMarketMovement>();
        foreach (var date in valuesByDate.Keys.Order())
        {
            if (!valuesByDate.TryGetValue(date.AddDays(-1), out var previousValues))
            {
                continue;
            }

            var changes = valuesByDate[date]
                .Where(entry => previousValues.ContainsKey(entry.Key))
                .Select(entry => entry.Value - previousValues[entry.Key])
                .ToList();
            if (changes.Count == 0)
            {
                continue;
            }

            movements.Add(new DailyMarketMovement(
                date,
                changes.Sum(change => (long)change),
                changes.Count(change => change > 0),
                changes.Count(change => change < 0),
                changes.Count(change => change == 0)));
        }

        if (movements.Count == 0)
        {
            return movements;
        }

        var maximumPlayerCount = movements.Max(movement => movement.PlayerCount);
        return [.. movements.Where(movement => movement.PlayerCount >= maximumPlayerCount * MinimumCoverageShare)];
    }

    public static int CountStreak(IReadOnlyList<DailyMarketMovement> movements)
    {
        if (movements.Count == 0)
        {
            return 0;
        }

        var latestSign = Math.Sign(movements[^1].TotalChange);
        if (latestSign == 0)
        {
            return 0;
        }

        var streak = 1;
        for (var index = movements.Count - 2; index >= 0; index--)
        {
            var isConsecutiveDay = movements[index].Date == movements[index + 1].Date.AddDays(-1);
            if (!isConsecutiveDay || Math.Sign(movements[index].TotalChange) != latestSign)
            {
                break;
            }

            streak++;
        }

        return streak;
    }

    public static IReadOnlyList<PlayerValueChange> ComputeChanges(
        IReadOnlyList<MarketValuePoint> points, DateOnly date, int days)
    {
        var baselineDate = date.AddDays(-days);
        var baselineValues = points
            .Where(point => point.Date == baselineDate)
            .ToDictionary(point => point.PlayerId, point => point.MarketValue);

        return [.. points
            .Where(point => point.Date == date && baselineValues.ContainsKey(point.PlayerId))
            .Select(point => new PlayerValueChange(point.PlayerId, point.MarketValue, point.MarketValue - baselineValues[point.PlayerId]))];
    }
}
