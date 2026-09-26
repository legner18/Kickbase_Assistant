namespace KickbaseAssistant.Domain.Players;

/// <summary>
/// Applies the exact same three-signal scoring TransferMarketScanJob already computes for market
/// players (momentum/lineup/fixtures) to the user's own squad instead - "should I consider selling
/// this player" rather than "should I buy this one". Same full-refresh-per-run semantics as
/// MarketScanResult: old rows for a league are cleared and rewritten each scan.
///
/// A LOW OverallScore here means bad momentum/lineup/fixtures for an owned player - worth
/// reviewing for a sale while the market value is still good, mirroring how a market player with a
/// HIGH score is worth buying.
/// </summary>
public class SquadPlayerScore
{
    public long Id { get; set; }
    public required string LeagueId { get; set; }
    public required string PlayerId { get; set; }
    public Player? Player { get; set; }

    public double MomentumScore { get; set; }
    public double LineupScore { get; set; }
    public double FixtureScore { get; set; }
    public double OverallScore { get; set; }

    public double? MarketValueChangeAbs { get; set; }
    public double? MarketValueChangePercent { get; set; }

    public DateTimeOffset ScannedAt { get; set; }
}
