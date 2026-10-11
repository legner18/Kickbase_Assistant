namespace KickbaseAssistant.Domain.Players;

public enum SellAlertSignal
{
    MarketValueDrop,
    LineupDowngrade,
    InjuryNews,
    TrendFlattening,
}

public class SellAlert
{
    public long Id { get; set; }
    public required string PlayerId { get; set; }
    public Player? Player { get; set; }
    public SellAlertSignal Signal { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool Acknowledged { get; set; }
}
