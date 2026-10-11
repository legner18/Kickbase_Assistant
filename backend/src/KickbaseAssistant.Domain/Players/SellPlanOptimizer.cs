namespace KickbaseAssistant.Domain.Players;

public readonly record struct SellCandidate(
    string PlayerId, string PlayerName, int Position, int MarketValue, double ExpectedPoints, bool Locked);

public readonly record struct SellCombination(
    IReadOnlyList<string> PlayerIds, int Proceeds, double LostExpectedPoints, int RemainingSquadSize, double ResultingBalance);

/// <summary>
/// Umbauplan "Verkaufsplaner mit Kader-Constraints": sucht die Verkaufs-Kombination(en), die den
/// Kontostand über eine Zielschwelle bringen und dabei am wenigsten erwartete Punkte kosten, statt
/// "alle Verkaufskandidaten raus" wie im Kickbase Calculator. Reine Empfehlung - kein Sofortverkauf,
/// siehe Klassenkommentar.
///
/// Brute Force über alle Teilmengen der nicht gesperrten Spieler, wie vom Plan selbst für
/// 10-15 Spieler vorgeschlagen ("Knapsack-Variante ... reicht Brute Force"). Oberhalb von
/// <see cref="MaxBruteForceCandidates"/> wird nicht mehr vollständig durchsucht (2^n würde
/// explodieren) - für reguläre Kickbase-Kadergrößen (≤ 20) nie relevant.
///
/// Wichtig (explizite Plan-Vorgabe): Kaufpreis/Verlust-Gewinn fließen bewusst NICHT ein - das wäre
/// Sunk Cost. Nur aktueller Marktwert und erwartete Punkte zählen.
/// </summary>
public static class SellPlanOptimizer
{
    public const int MaxBruteForceCandidates = 20;

    public static IReadOnlyList<SellCombination> FindBestCombinations(
        IReadOnlyList<SellCandidate> squad,
        double currentBalance,
        double targetBalance,
        int maxResults = 3)
    {
        var sellable = squad.Where(p => !p.Locked).ToList();
        if (sellable.Count > MaxBruteForceCandidates)
        {
            sellable = [.. sellable.OrderByDescending(p => p.MarketValue).Take(MaxBruteForceCandidates)];
        }

        var n = sellable.Count;
        var results = new List<SellCombination>();

        for (var mask = 0; mask < (1 << n); mask++)
        {
            var chosen = new List<SellCandidate>();
            for (var i = 0; i < n; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    chosen.Add(sellable[i]);
                }
            }

            var proceeds = chosen.Sum(c => c.MarketValue);
            var resultingBalance = currentBalance + proceeds;
            if (resultingBalance < targetBalance)
            {
                continue;
            }

            var chosenIds = chosen.Select(c => c.PlayerId).ToHashSet();
            var remainingPositions = squad.Where(p => !chosenIds.Contains(p.PlayerId)).Select(p => p.Position).ToList();
            if (RosterCheck.FindGaps(remainingPositions).Count > 0)
            {
                continue;
            }

            results.Add(new SellCombination(
                [.. chosenIds], proceeds, chosen.Sum(c => c.ExpectedPoints), squad.Count - chosen.Count, resultingBalance));
        }

        return results
            .OrderBy(r => r.LostExpectedPoints)
            .ThenByDescending(r => r.RemainingSquadSize)
            .Take(maxResults)
            .ToList();
    }
}
