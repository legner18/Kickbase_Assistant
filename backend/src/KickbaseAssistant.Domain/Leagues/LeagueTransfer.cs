namespace KickbaseAssistant.Domain.Leagues;

public enum TransferType
{
    Buy,
    Sell,
}

/// <summary>
/// One row per observed transfer for a league manager (append-only log, deduped on insert by
/// ManagerId+PlayerId+Type+OccurredAt). No FK to Player - a manager's transfer history can
/// reference any Bundesliga player ever, not just ones we happen to track in Players.
/// MarketValueAtTransfer/OverpayPercent are only filled for recent Buy transfers where a market-
/// value history lookup was actually attempted (see LeagueAnalysisJob) - older or Sell rows keep
/// them null rather than guessing.
/// </summary>
public class LeagueTransfer
{
    public long Id { get; set; }
    public required string LeagueId { get; set; }
    public required string ManagerId { get; set; }
    public required string ManagerName { get; set; }
    public required string PlayerId { get; set; }
    public required string PlayerName { get; set; }
    public TransferType Type { get; set; }
    public int Price { get; set; }
    public string? OtherManagerName { get; set; }
    public double? MarketValueAtTransfer { get; set; }
    public double? OverpayPercent { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
