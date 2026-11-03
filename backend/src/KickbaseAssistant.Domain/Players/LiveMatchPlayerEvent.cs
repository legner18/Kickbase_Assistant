namespace KickbaseAssistant.Domain.Players;

public class LiveMatchPlayerEvent
{
    public long Id { get; set; }
    public long LiveMatchPlayerId { get; set; }
    public required string EventId { get; set; }
    public int EventTypeId { get; set; }
    public string? Title { get; set; }
    public int Points { get; set; }
    public int Minute { get; set; }
    public LiveMatchPlayer? Player { get; set; }
}
