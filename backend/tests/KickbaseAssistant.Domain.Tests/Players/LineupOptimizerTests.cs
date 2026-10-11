using KickbaseAssistant.Domain.Players;
using Xunit;

namespace KickbaseAssistant.Domain.Tests.Players;

public class LineupOptimizerTests
{
    private static LineupCandidate Candidate(string id, int position, double expectedPoints, double startProbability = 1.0, bool injured = false) =>
        new(id, $"Player {id}", position, expectedPoints, startProbability, injured);

    // Breiter Kader: genug Spieler für jede Standard-Formation (bis zu 5 ABW/MF, 3 ST, 1 TW).
    private static List<LineupCandidate> BuildDeepSquad()
    {
        var squad = new List<LineupCandidate> { Candidate("tw1", 1, 50) };
        for (var i = 1; i <= 5; i++)
        {
            squad.Add(Candidate($"ab{i}", 2, 40 + i));
            squad.Add(Candidate($"mf{i}", 3, 40 + i));
        }
        for (var i = 1; i <= 3; i++)
        {
            squad.Add(Candidate($"st{i}", 4, 40 + i));
        }
        return squad;
    }

    [Fact]
    public void FindBestLineup_DeepSquad_ReturnsElevenPlayers()
    {
        var result = LineupOptimizer.FindBestLineup(BuildDeepSquad());

        Assert.NotNull(result);
        Assert.Equal(11, result!.Value.PlayerIds.Count);
    }

    [Fact]
    public void FindBestLineup_PicksHighestWeightedPointsAmongEligiblePlayers()
    {
        var squad = BuildDeepSquad();
        // st3 hat die höchsten erwarteten Punkte in der Gruppe - muss in jeder gewählten Formation
        // mit mindestens 1 Stürmer enthalten sein.
        var result = LineupOptimizer.FindBestLineup(squad);

        Assert.Contains("st3", result!.Value.PlayerIds);
    }

    [Fact]
    public void FindBestLineup_WeightsByStartProbability()
    {
        // Erfüllt genau die 4-5-1-Formation (bestes Ergebnis mit nur 1 Sturm-Slot): zwei
        // Sturm-Kandidaten konkurrieren um diesen einen Platz.
        var squad = new List<LineupCandidate>
        {
            Candidate("tw1", 1, 50),
            Candidate("ab1", 2, 40), Candidate("ab2", 2, 40), Candidate("ab3", 2, 40), Candidate("ab4", 2, 40),
            Candidate("mf1", 3, 40), Candidate("mf2", 3, 40), Candidate("mf3", 3, 40), Candidate("mf4", 3, 40), Candidate("mf5", 3, 40),
            // Hohe Rohpunkte, aber praktisch nie im Kader -> sollte dem sicheren Starter unterliegen.
            Candidate("st1", 4, 100, startProbability: 0.1),
            Candidate("st2", 4, 30, startProbability: 1.0),
        };

        var result = LineupOptimizer.FindBestLineup(squad);

        Assert.Contains("st2", result!.Value.PlayerIds);
        Assert.DoesNotContain("st1", result!.Value.PlayerIds);
    }

    [Fact]
    public void FindBestLineup_InjuredPlayerStillEligible()
    {
        // Exakt 3-4-3 ohne Reserve pro Position, damit alle 3 ABW (inkl. des verletzten) gebraucht
        // werden - beweist, dass Injured allein niemanden ausschließt.
        var squad = new List<LineupCandidate>
        {
            Candidate("tw1", 1, 50),
            Candidate("ab1", 2, 40, injured: true), Candidate("ab2", 2, 40), Candidate("ab3", 2, 40),
            Candidate("mf1", 3, 40), Candidate("mf2", 3, 40), Candidate("mf3", 3, 40), Candidate("mf4", 3, 40),
            Candidate("st1", 4, 40), Candidate("st2", 4, 40), Candidate("st3", 4, 40),
        };

        var result = LineupOptimizer.FindBestLineup(squad);

        Assert.NotNull(result);
        Assert.Contains("ab1", result!.Value.PlayerIds);
    }

    [Fact]
    public void FindBestLineup_NotEnoughPlayersForAnyFormation_ReturnsNull()
    {
        var squad = new List<LineupCandidate> { Candidate("tw1", 1, 50), Candidate("ab1", 2, 40) };

        var result = LineupOptimizer.FindBestLineup(squad);

        Assert.Null(result);
    }

    [Fact]
    public void FindUnfillablePositions_MinimalValidSquad_ReportsNoGaps()
    {
        var squad = new List<LineupCandidate>
        {
            Candidate("tw1", 1, 50),
            Candidate("ab1", 2, 40), Candidate("ab2", 2, 40), Candidate("ab3", 2, 40),
            Candidate("mf1", 3, 40), Candidate("mf2", 3, 40),
            Candidate("st1", 4, 40),
        };

        Assert.Empty(LineupOptimizer.FindUnfillablePositions(squad));
    }

    [Fact]
    public void FindClosestFormation_OneMidfielderShort_ReportsExactGap()
    {
        // Entspricht dem real beobachteten Bousching-Kader: viele ABW/ST, nur 2 MF - jede
        // Standard-Formation braucht mindestens 3, am nächsten liegt die mit nur 1 fehlendem MF.
        var squad = new List<LineupCandidate> { Candidate("tw1", 1, 50) };
        for (var i = 1; i <= 7; i++)
        {
            squad.Add(Candidate($"ab{i}", 2, 40));
        }
        squad.Add(Candidate("mf1", 3, 40));
        squad.Add(Candidate("mf2", 3, 40));
        for (var i = 1; i <= 5; i++)
        {
            squad.Add(Candidate($"st{i}", 4, 40));
        }

        Assert.Null(LineupOptimizer.FindBestLineup(squad));

        var (formation, missing) = LineupOptimizer.FindClosestFormation(squad);

        var missingEntry = Assert.Single(missing);
        Assert.Equal(3, missingEntry.Key); // Mittelfeld
        Assert.Equal(1, missingEntry.Value);
        Assert.True(formation.Midfielders is 3); // 4-3-3 oder 5-3-2, beide mit minimalem MF-Bedarf
    }

    [Fact]
    public void FindUnfillablePositions_MissingForward_ReportsSturmGap()
    {
        var squad = new List<LineupCandidate>
        {
            Candidate("tw1", 1, 50),
            Candidate("ab1", 2, 40), Candidate("ab2", 2, 40), Candidate("ab3", 2, 40),
            Candidate("mf1", 3, 40), Candidate("mf2", 3, 40),
        };

        var gaps = LineupOptimizer.FindUnfillablePositions(squad);

        Assert.Contains(gaps, g => g.Contains("Sturm"));
    }
}
