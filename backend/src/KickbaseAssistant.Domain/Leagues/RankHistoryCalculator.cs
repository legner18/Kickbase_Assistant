namespace KickbaseAssistant.Domain.Leagues;

public readonly record struct ManagerMatchdayPoints(string ManagerId, int MatchdayNumber, int Points);

public readonly record struct ManagerStanding(
    string ManagerId, int MatchdayNumber, int MatchdayPoints, int CumulativePoints, int Rank);

public static class RankHistoryCalculator
{
    public static IReadOnlyList<ManagerStanding> Compute(IReadOnlyList<ManagerMatchdayPoints> results)
    {
        var managerIds = results.Select(result => result.ManagerId).Distinct().ToList();
        var pointsByMatchday = results
            .GroupBy(result => result.MatchdayNumber)
            .OrderBy(matchdayGroup => matchdayGroup.Key)
            .ToList();

        var cumulativePoints = managerIds.ToDictionary(managerId => managerId, managerId => 0);
        var standings = new List<ManagerStanding>();

        foreach (var matchdayGroup in pointsByMatchday)
        {
            var matchdayPoints = matchdayGroup.ToDictionary(result => result.ManagerId, result => result.Points);
            foreach (var managerId in managerIds)
            {
                cumulativePoints[managerId] += matchdayPoints.GetValueOrDefault(managerId);
            }

            foreach (var managerId in managerIds)
            {
                var rank = 1 + cumulativePoints.Values.Count(points => points > cumulativePoints[managerId]);
                standings.Add(new ManagerStanding(
                    managerId,
                    matchdayGroup.Key,
                    matchdayPoints.GetValueOrDefault(managerId),
                    cumulativePoints[managerId],
                    rank));
            }
        }

        return standings;
    }
}
