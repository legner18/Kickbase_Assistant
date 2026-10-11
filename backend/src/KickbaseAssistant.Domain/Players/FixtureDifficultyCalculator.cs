namespace KickbaseAssistant.Domain.Players;

public readonly record struct FixtureMatch(int Day, string HomeTeamId, string AwayTeamId);

public readonly record struct UpcomingFixture(int Day, string OpponentTeamId, bool IsHome, int DifficultyTier);

/// <summary>
/// Gegnerstärke/Restprogramm (Umbauplan): reine Rechenlogik, keine Kickbase-DTOs, damit sie sich
/// ohne HTTP/DB testen lässt. <see cref="ComputeScore"/> ist unverändert die Logik, die vorher
/// direkt in TransferMarketScanJob lag (0-100, 3 Spiele Lookahead) - für MarketScanResult/
/// SquadPlayerScore bleibt das Verhalten identisch. <see cref="ComputeUpcomingFixtures"/> ist die
/// neue, feinere Ansicht für die Spieler-Detailseite (5 Spiele, 5-stufige Schwierigkeit,
/// Heim/Auswärts).
///
/// Die Kickbase-API liefert keine getrennten Tore-geschossen/-kassiert-Werte je Team (nur
/// Tabellenplatz und Tordifferenz als Summe) - Offensiv- und Defensiv-Schwierigkeit lassen sich
/// damit nicht sauber trennen. Beide Methoden liefern deshalb eine einzelne, auf dem Tabellenplatz
/// basierende Schwierigkeit statt einer (nicht belegbaren) Offensive/Defensive-Aufteilung.
/// </summary>
public static class FixtureDifficultyCalculator
{
    public const int ScoreLookaheadMatches = 3;
    public const int DisplayLookaheadMatches = 5;

    public static double ComputeScore(
        string teamId,
        IReadOnlyList<FixtureMatch> matches,
        int currentMatchday,
        IReadOnlyDictionary<string, int> placements,
        int lookahead = ScoreLookaheadMatches)
    {
        var opponentPlacements = UpcomingOpponents(teamId, matches, currentMatchday, lookahead)
            .Where(placements.ContainsKey)
            .Select(id => placements[id])
            .ToList();

        if (opponentPlacements.Count == 0 || placements.Count == 0)
        {
            return 50;
        }

        return Math.Clamp(opponentPlacements.Average() / placements.Count * 100, 0, 100);
    }

    public static IReadOnlyList<UpcomingFixture> ComputeUpcomingFixtures(
        string teamId,
        IReadOnlyList<FixtureMatch> matches,
        int currentMatchday,
        IReadOnlyDictionary<string, int> placements,
        int lookahead = DisplayLookaheadMatches)
    {
        var teamCount = placements.Count;
        var upcoming = matches
            .Where(m => m.Day > currentMatchday && (m.HomeTeamId == teamId || m.AwayTeamId == teamId))
            .OrderBy(m => m.Day)
            .Take(lookahead);

        var result = new List<UpcomingFixture>();
        foreach (var m in upcoming)
        {
            var isHome = m.HomeTeamId == teamId;
            var opponentId = isHome ? m.AwayTeamId : m.HomeTeamId;
            var tier = teamCount > 0 && placements.TryGetValue(opponentId, out var placement)
                ? DifficultyTier(placement, teamCount)
                : 3;
            result.Add(new UpcomingFixture(m.Day, opponentId, isHome, tier));
        }

        return result;
    }

    private static IEnumerable<string> UpcomingOpponents(
        string teamId, IReadOnlyList<FixtureMatch> matches, int currentMatchday, int lookahead) =>
        matches
            .Where(m => m.Day > currentMatchday && (m.HomeTeamId == teamId || m.AwayTeamId == teamId))
            .OrderBy(m => m.Day)
            .Take(lookahead)
            .Select(m => m.HomeTeamId == teamId ? m.AwayTeamId : m.HomeTeamId);

    /// <summary>Tier 5 = sehr schwer (Tabellenführer), Tier 1 = sehr leicht (Tabellenletzter).</summary>
    private static int DifficultyTier(int placement, int teamCount)
    {
        var quantile = (placement - 1) / (double)teamCount;
        var tier = 5 - (int)Math.Floor(quantile * 5);
        return Math.Clamp(tier, 1, 5);
    }
}
