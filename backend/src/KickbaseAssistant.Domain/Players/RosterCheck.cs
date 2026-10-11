namespace KickbaseAssistant.Domain.Players;

/// <summary>
/// Kader-Check aus dem Umbauplan: prüft, ob der Kader die Mindestbesetzung für eine aufstellbare
/// Elf hergibt (1 TW, 3 ABW, 2 MF, 1 ST - die defensivste zulässige Kickbase-Formation). Ein
/// verletzter Spieler zählt als besetzt, es wird nur auf Positionsnummer geprüft - daher bewusst
/// kein Statuscode-Parameter.
/// </summary>
public static class RosterCheck
{
    private static readonly (int Position, int MinCount, string Label)[] Requirements =
    [
        (1, 1, "Torwart"),
        (2, 3, "Abwehr"),
        (3, 2, "Mittelfeld"),
        (4, 1, "Sturm"),
    ];

    public static IReadOnlyList<string> FindGaps(IReadOnlyCollection<int> squadPositions)
    {
        var counts = squadPositions.GroupBy(p => p).ToDictionary(g => g.Key, g => g.Count());
        var gaps = new List<string>();

        foreach (var (position, minCount, label) in Requirements)
        {
            var have = counts.GetValueOrDefault(position, 0);
            if (have < minCount)
            {
                gaps.Add($"{label}: {have}/{minCount} besetzt");
            }
        }

        return gaps;
    }
}
