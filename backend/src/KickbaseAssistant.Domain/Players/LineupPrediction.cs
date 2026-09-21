namespace KickbaseAssistant.Domain.Players;

public enum LineupStatus
{
    Starting,
    Bench,
    Doubtful,
    Out,
}

public class LineupPrediction
{
    public long Id { get; set; }
    public required string PlayerId { get; set; }
    public Player? Player { get; set; }
    public int MatchdayNumber { get; set; }
    public LineupStatus Status { get; set; }
    public required string Source { get; set; }
    public DateTimeOffset ScrapedAt { get; set; }
}
