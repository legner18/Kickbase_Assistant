namespace KickbaseAssistant.Domain.Players;

public readonly record struct Formation(int Defenders, int Midfielders, int Forwards)
{
    public override string ToString() => $"{Defenders}-{Midfielders}-{Forwards}";
}

public readonly record struct LineupCandidate(
    string PlayerId, string PlayerName, int Position, double ExpectedPoints, double StartProbability, bool Injured)
{
    public double WeightedPoints => ExpectedPoints * StartProbability;
}

public readonly record struct LineupResult(Formation Formation, IReadOnlyList<string> PlayerIds, double TotalExpectedPoints);

/// <summary>
/// Umbauplan "Aufstellungs-Optimierer": wählt die Elf mit den meisten erwarteten Punkten,
/// gewichtet mit der Startwahrscheinlichkeit ("Erwartete Punkte = Prognose × Startwahrscheinlichkeit"
/// - Wackelkandidaten landen so nicht vor sicheren Startern). Nutzt dieselbe Ø-letzte-5-Spiele-
/// Baseline wie der Verkaufsplaner, nicht die (noch nicht existierende) ML-Prognose aus Phase 3 -
/// der Plan selbst sagt, dass Verkaufsplaner UND Optimierer von Anfang an mit der Baseline laufen
/// und ML sie erst ersetzt, wenn sie die Baseline nachweislich schlägt.
///
/// Die Kickbase-API liefert keine Liste erlaubter Formationen (kein Endpoint dafür gefunden) -
/// <see cref="StandardFormations"/> ist deshalb der bekannte Standard-Satz an Kickbase-Formationen,
/// nicht live gegen einen Endpoint verifiziert.
///
/// Ein verletzter Spieler (<see cref="LineupCandidate.Injured"/>) wird bewusst NICHT ausgeschlossen
/// - "lieber einen Verletzten notfalls aufstellen als eine Position leer lassen" laut Plan.
/// </summary>
public static class LineupOptimizer
{
    public static readonly IReadOnlyList<Formation> StandardFormations =
    [
        new(3, 4, 3),
        new(3, 5, 2),
        new(4, 3, 3),
        new(4, 4, 2),
        new(4, 5, 1),
        new(5, 3, 2),
        new(5, 4, 1),
    ];

    public static LineupResult? FindBestLineup(IReadOnlyList<LineupCandidate> squad)
    {
        var byPosition = squad
            .GroupBy(c => c.Position)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.WeightedPoints).ToList());

        LineupResult? best = null;

        foreach (var formation in StandardFormations)
        {
            var requirement = new (int Position, int Count)[]
            {
                (1, 1), (2, formation.Defenders), (3, formation.Midfielders), (4, formation.Forwards),
            };

            var selected = new List<LineupCandidate>();
            var feasible = true;

            foreach (var (position, count) in requirement)
            {
                if (!byPosition.TryGetValue(position, out var available) || available.Count < count)
                {
                    feasible = false;
                    break;
                }

                selected.AddRange(available.Take(count));
            }

            if (!feasible)
            {
                continue;
            }

            var total = selected.Sum(c => c.WeightedPoints);
            if (best is null || total > best.Value.TotalExpectedPoints)
            {
                best = new LineupResult(formation, [.. selected.Select(c => c.PlayerId)], total);
            }
        }

        return best;
    }

    /// <summary>
    /// Für "weniger als 11 Spieler aufstellbar": Mindestbesetzung (1 TW, 3 ABW, 2 MF, 1 ST - die
    /// defensivste zulässige Formation, siehe RosterCheck) gegen den Kader geprüft; liefert die
    /// Positionen, bei denen selbst das Minimum nicht erreicht wird.
    /// </summary>
    public static IReadOnlyList<string> FindUnfillablePositions(IReadOnlyList<LineupCandidate> squad) =>
        RosterCheck.FindGaps([.. squad.Select(c => c.Position)]);

    /// <summary>
    /// Wenn FindBestLineup null liefert (Kader erfüllt die lockere RosterCheck-Mindestbesetzung,
    /// aber keine der 11-Spieler-Standard-Formationen): findet die Formation mit dem geringsten
    /// Fehlbedarf und meldet genau, wie viele Spieler welcher Position noch fehlen würden.
    /// </summary>
    public static (Formation Formation, IReadOnlyDictionary<int, int> MissingByPosition) FindClosestFormation(
        IReadOnlyList<LineupCandidate> squad)
    {
        var counts = squad.GroupBy(c => c.Position).ToDictionary(g => g.Key, g => g.Count());

        Formation? bestFormation = null;
        Dictionary<int, int>? bestMissing = null;
        var bestTotalMissing = int.MaxValue;

        foreach (var formation in StandardFormations)
        {
            var requirement = new Dictionary<int, int> { [1] = 1, [2] = formation.Defenders, [3] = formation.Midfielders, [4] = formation.Forwards };
            var missing = requirement
                .Select(kv => (Position: kv.Key, Missing: Math.Max(0, kv.Value - counts.GetValueOrDefault(kv.Key, 0))))
                .Where(x => x.Missing > 0)
                .ToDictionary(x => x.Position, x => x.Missing);

            var total = missing.Values.Sum();
            if (total < bestTotalMissing)
            {
                bestTotalMissing = total;
                bestFormation = formation;
                bestMissing = missing;
            }
        }

        return (bestFormation!.Value, bestMissing!);
    }
}
