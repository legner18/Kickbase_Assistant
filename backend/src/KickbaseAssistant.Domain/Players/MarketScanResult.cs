namespace KickbaseAssistant.Domain.Players;

/// <summary>
/// One row per player currently listed on a league's transfer market, refreshed in full each
/// scan run (old rows for the league are cleared first - this is a snapshot of "what's on the
/// market right now", not a history log). Score fields are 0-100, equally weighted into
/// OverallScore; see TransferMarketScanJob for the exact formulas.
/// </summary>
public class MarketScanResult
{
    public long Id { get; set; }
    public required string LeagueId { get; set; }
    public required string PlayerId { get; set; }
    public Player? Player { get; set; }

    public int Price { get; set; }
    public int OfferCount { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public double MomentumScore { get; set; }
    public double LineupScore { get; set; }
    public double FixtureScore { get; set; }
    public double OverallScore { get; set; }

    public DateTimeOffset ScannedAt { get; set; }
}
