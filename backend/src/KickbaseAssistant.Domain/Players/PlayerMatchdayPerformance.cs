namespace KickbaseAssistant.Domain.Players;

/// <summary>
/// Ein Spieltag eines Spielers (Umbauplan "Datenbasis: tägliche Snapshots", Tabelle
/// player_matchday) - Grundlage für Median, S11-Quote und Pkt/Min, die sich aus
/// Player.TotalPoints/AveragePoints (Saison-Aggregate) nicht berechnen lassen.
///
/// SeasonId ist Teil des Schlüssels, weil Kickbases Spieltagsnummer ("day") pro Saison bei 1
/// neu zählt - ohne Saison wären Spieltag 1 der Vorsaison und Spieltag 1 der laufenden Saison
/// nicht unterscheidbar.
///
/// StartedInLineup ist eine Annäherung (Minuten ≥ 60), da die Kickbase-API kein explizites
/// Startelf-Flag pro Spieltag liefert, nur gespielte Minuten.
/// </summary>
public class PlayerMatchdayPerformance
{
    public long Id { get; set; }
    public required string PlayerId { get; set; }
    public Player? Player { get; set; }
    public required string SeasonId { get; set; }
    public int MatchdayNumber { get; set; }
    public int Points { get; set; }
    public int MinutesPlayed { get; set; }
    public bool StartedInLineup { get; set; }
}
