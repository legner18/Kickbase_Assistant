using KickbaseAssistant.Domain.Players;
using Xunit;

namespace KickbaseAssistant.Domain.Tests.Players;

public class FixtureDifficultyCalculatorTests
{
    // 5 Teams, Platz 1 (A) = stärkster Gegner, Platz 5 (E) = schwächster.
    private static readonly Dictionary<string, int> Placements = new()
    {
        ["A"] = 1,
        ["B"] = 2,
        ["C"] = 3,
        ["D"] = 4,
        ["E"] = 5,
    };

    [Fact]
    public void ComputeUpcomingFixtures_OrdersByDayAndRespectsLookahead()
    {
        var matches = new[]
        {
            new FixtureMatch(2, "X", "B"),
            new FixtureMatch(1, "X", "A"),
            new FixtureMatch(3, "C", "X"),
        };

        var fixtures = FixtureDifficultyCalculator.ComputeUpcomingFixtures("X", matches, currentMatchday: 0, Placements, lookahead: 2);

        Assert.Equal(2, fixtures.Count);
        Assert.Equal(1, fixtures[0].Day);
        Assert.Equal("A", fixtures[0].OpponentTeamId);
        Assert.Equal(2, fixtures[1].Day);
        Assert.Equal("B", fixtures[1].OpponentTeamId);
    }

    [Fact]
    public void ComputeUpcomingFixtures_SetsIsHomeCorrectly()
    {
        var matches = new[]
        {
            new FixtureMatch(1, "X", "A"), // X spielt zuhause
            new FixtureMatch(2, "C", "X"), // X spielt auswärts
        };

        var fixtures = FixtureDifficultyCalculator.ComputeUpcomingFixtures("X", matches, currentMatchday: 0, Placements);

        Assert.True(fixtures[0].IsHome);
        Assert.False(fixtures[1].IsHome);
    }

    [Fact]
    public void ComputeUpcomingFixtures_TopPlacedOpponent_IsHardestTier()
    {
        var matches = new[] { new FixtureMatch(1, "X", "A") };

        var fixtures = FixtureDifficultyCalculator.ComputeUpcomingFixtures("X", matches, currentMatchday: 0, Placements);

        Assert.Equal(5, fixtures[0].DifficultyTier);
    }

    [Fact]
    public void ComputeUpcomingFixtures_BottomPlacedOpponent_IsEasiestTier()
    {
        var matches = new[] { new FixtureMatch(1, "X", "E") };

        var fixtures = FixtureDifficultyCalculator.ComputeUpcomingFixtures("X", matches, currentMatchday: 0, Placements);

        Assert.Equal(1, fixtures[0].DifficultyTier);
    }

    [Fact]
    public void ComputeUpcomingFixtures_IgnoresPastMatchdays()
    {
        var matches = new[]
        {
            new FixtureMatch(1, "X", "A"),
            new FixtureMatch(2, "X", "B"),
        };

        var fixtures = FixtureDifficultyCalculator.ComputeUpcomingFixtures("X", matches, currentMatchday: 1, Placements);

        Assert.Single(fixtures);
        Assert.Equal("B", fixtures[0].OpponentTeamId);
    }

    [Fact]
    public void ComputeScore_NoUpcomingMatches_ReturnsNeutral50()
    {
        var score = FixtureDifficultyCalculator.ComputeScore("X", [], currentMatchday: 0, Placements);

        Assert.Equal(50, score);
    }

    [Fact]
    public void ComputeScore_ToughestOpponent_IsLowScore()
    {
        var matches = new[] { new FixtureMatch(1, "X", "A") };

        var score = FixtureDifficultyCalculator.ComputeScore("X", matches, currentMatchday: 0, Placements);

        // Platz 1 von 5 Teams -> 1/5 * 100 = 20
        Assert.Equal(20, score, precision: 0);
    }
}
