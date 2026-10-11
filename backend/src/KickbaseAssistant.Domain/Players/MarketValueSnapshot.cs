namespace KickbaseAssistant.Domain.Players;

public class MarketValueSnapshot
{
    public long Id { get; set; }
    public required string PlayerId { get; set; }
    public Player? Player { get; set; }
    public DateOnly Date { get; set; }
    public int MarketValue { get; set; }
    public int MarketValueChange { get; set; }
    public int StatusCode { get; set; }
}
