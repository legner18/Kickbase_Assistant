namespace KickbaseAssistant.Domain.Players;

public class LiveMatchPlayer
{
    public long Id { get; set; }
    public required string MatchId { get; set; }
    public required string TeamId { get; set; }
    public required string PlayerId { get; set; }
    public required string PlayerName { get; set; }
    public int Points { get; set; }
    public int[] EventKeys { get; set; } = [];
    public int? TimelinePoints { get; set; }
    public DateTimeOffset? TimelineFetchedAt { get; set; }
    public LiveMatch? Match { get; set; }

    public ICollection<LiveMatchPlayerEvent> Events { get; set; } = [];
}
