namespace KickbaseAssistant.Domain.Players;

public enum MarketListingOutcome
{
    Open,
    Sold,
    Expired,
}

/// <summary>
/// Umbauplan "Liga-Insights": "wann ein Spieler in deiner Liga angeboten wurde und wie es ausging -
/// Kickbase zeigt das nirgends". Von LiveMarketJob geschrieben: ein Open-Eintrag beginnt, sobald
/// MarketChangeDetector eine neue Listung sieht, und wird geschlossen, sobald sie verschwindet.
/// Sold vs. Expired ist ein Best-Effort-Abgleich gegen die im selben Lauf ohnehin schon geladene
/// Activity-Feed-Liste (siehe LiveMarketJob.DetermineOutcome) - kein garantiert exaktes Matching,
/// aber die einzige verfügbare Quelle ohne zusätzliche Kickbase-Aufrufe.
/// </summary>
public class MarketListingHistory
{
    public long Id { get; set; }
    public required string LeagueId { get; set; }
    public required string PlayerId { get; set; }
    public required string PlayerName { get; set; }
    public DateTimeOffset ListedAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
    public int Price { get; set; }
    public MarketListingOutcome Outcome { get; set; }
}
