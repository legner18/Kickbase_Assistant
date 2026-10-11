using KickbaseAssistant.Domain.Players;
using Xunit;

namespace KickbaseAssistant.Domain.Tests.Players;

public class RosterCheckTests
{
    [Fact]
    public void FindGaps_MinimumValidSquad_HasNoGaps()
    {
        // 1 TW (1), 3 ABW (2), 2 MF (3), 1 ST (4)
        var positions = new[] { 1, 2, 2, 2, 3, 3, 4 };

        var gaps = RosterCheck.FindGaps(positions);

        Assert.Empty(gaps);
    }

    [Fact]
    public void FindGaps_MissingDefender_ReportsAbwehrGap()
    {
        var positions = new[] { 1, 2, 2, 3, 3, 4 };

        var gaps = RosterCheck.FindGaps(positions);

        Assert.Single(gaps);
        Assert.Contains("Abwehr", gaps[0]);
    }

    [Fact]
    public void FindGaps_EmptySquad_ReportsAllFourGaps()
    {
        var gaps = RosterCheck.FindGaps([]);

        Assert.Equal(4, gaps.Count);
    }

    [Fact]
    public void FindGaps_ExtraPlayersBeyondMinimum_StillNoGaps()
    {
        // Großzügiger Kader mit mehr als dem Minimum pro Position.
        var positions = new[] { 1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 4, 4 };

        var gaps = RosterCheck.FindGaps(positions);

        Assert.Empty(gaps);
    }
}
