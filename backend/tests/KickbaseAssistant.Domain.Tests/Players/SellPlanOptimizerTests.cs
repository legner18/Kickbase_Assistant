using KickbaseAssistant.Domain.Players;
using Xunit;

namespace KickbaseAssistant.Domain.Tests.Players;

public class SellPlanOptimizerTests
{
    // Minimal aufstellbarer Kader (1 TW, 3 ABW, 2 MF, 1 ST) plus genau zwei "Luxus"-Spieler
    // (ab4, st2), die über das Minimum hinausgehen - nur diese beiden dürfen einzeln verkauft
    // werden, ohne eine Positions-Lücke zu reißen.
    private static List<SellCandidate> BuildSquad() =>
    [
        new("tw1", "TW1", 1, 2_000_000, 50, false),
        new("ab1", "ABW1", 2, 3_000_000, 60, false),
        new("ab2", "ABW2", 2, 3_000_000, 55, false),
        new("ab3", "ABW3", 2, 3_000_000, 45, false),
        new("ab4", "ABW4", 2, 1_500_000, 30, false), // Luxus-ABW
        new("mf1", "MF1", 3, 4_000_000, 70, false),
        new("mf2", "MF2", 3, 4_000_000, 65, false),
        new("st1", "ST1", 4, 5_000_000, 80, false),
        new("st2", "ST2", 4, 2_000_000, 20, false), // Luxus-ST
    ];

    [Fact]
    public void FindBestCombinations_AlreadyAboveTarget_IncludesEmptyCombination()
    {
        var squad = BuildSquad();

        var results = SellPlanOptimizer.FindBestCombinations(squad, currentBalance: 1_000_000, targetBalance: 0);

        Assert.Contains(results, r => r.PlayerIds.Count == 0 && r.LostExpectedPoints == 0);
    }

    [Fact]
    public void FindBestCombinations_NegativeBalance_OnlyReturnsCombinationsMeetingTarget()
    {
        var squad = BuildSquad();

        var results = SellPlanOptimizer.FindBestCombinations(squad, currentBalance: -1_500_000, targetBalance: 0);

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.ResultingBalance >= 0));
    }

    [Fact]
    public void FindBestCombinations_WouldNeedMandatoryPlayers_ReturnsEmptyRatherThanViolatingFormation()
    {
        var squad = BuildSquad();

        // Nur die beiden Luxus-Spieler (ab4+st2, zusammen 3,5 Mio) dürfen ohne Lücke verkauft
        // werden. Ein weit darüber liegender Zielbetrag ist nur über Pflichtspieler erreichbar -
        // das Ergebnis muss leer bleiben statt die Mindestformation zu brechen.
        var results = SellPlanOptimizer.FindBestCombinations(squad, currentBalance: -19_000_000, targetBalance: 0);

        Assert.Empty(results);
    }

    [Fact]
    public void FindBestCombinations_LockedPlayer_NeverIncludedButStillFindsAlternative()
    {
        var squad = BuildSquad();
        squad[8] = squad[8] with { Locked = true }; // st2 gesperrt - günstigster Verlust nicht verfügbar

        var results = SellPlanOptimizer.FindBestCombinations(squad, currentBalance: -1_500_000, targetBalance: 0);

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.DoesNotContain("st2", r.PlayerIds));
    }

    [Fact]
    public void FindBestCombinations_PrefersLowestLostPoints()
    {
        var squad = BuildSquad();

        // -1,5 Mio fehlen: st2 (2M/20Pkt) allein reicht und verliert weniger Punkte als ab4
        // (1,5M/30Pkt) allein oder beide zusammen - muss also vorne stehen.
        var results = SellPlanOptimizer.FindBestCombinations(squad, currentBalance: -1_500_000, targetBalance: 0);

        Assert.Equal(["st2"], results[0].PlayerIds);
    }

    [Fact]
    public void FindBestCombinations_NoFeasibleCombination_ReturnsEmptyList()
    {
        var squad = BuildSquad();

        // Unmöglich hoher Zielbetrag, selbst der gesamte Kaderwert reicht nicht.
        var results = SellPlanOptimizer.FindBestCombinations(squad, currentBalance: -1_000_000_000, targetBalance: 0);

        Assert.Empty(results);
    }
}
