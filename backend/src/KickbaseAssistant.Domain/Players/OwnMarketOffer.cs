namespace KickbaseAssistant.Domain.Players;

public class OwnMarketOffer
{
    public long Id { get; set; }
    public required string LeagueId { get; set; }
    public required string PlayerId { get; set; }
    public required string PlayerName { get; set; }
    public int OfferPrice { get; set; }
    public int MarketValue { get; set; }
    public int ListingPrice { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
