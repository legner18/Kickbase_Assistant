namespace KickbaseAssistant.Domain.Players;

/// <summary>
/// Nächste Gegner eines Teams (nicht eines einzelnen Spielers - alle Spieler desselben Teams
/// haben dasselbe Restprogramm). Von TransferMarketScanJob einmal pro Competition pro Lauf
/// komplett neu geschrieben (full refresh, wie MarketScanResult), für jedes Team der Competition,
/// nicht nur für Teams mit bekannten Spielern - damit die Spieler-Detailseite für jeden Spieler
/// sofort Daten hat, unabhängig davon ob er gerade im Kader/Markt eines Nutzers auftaucht.
/// </summary>
public class TeamFixture
{
    public long Id { get; set; }
    public required string CompetitionId { get; set; }
    public required string TeamId { get; set; }
    public int MatchdayNumber { get; set; }
    public required string OpponentTeamId { get; set; }
    public string? OpponentTeamName { get; set; }
    public bool IsHome { get; set; }
    public int DifficultyTier { get; set; }
}
