namespace KickbaseAssistant.Domain.Leagues;

public class ManagerMatchdayResult
{
    public long Id { get; set; }
    public required string LeagueId { get; set; }
    public required string ManagerId { get; set; }
    public required string ManagerName { get; set; }
    public int MatchdayNumber { get; set; }
    public int Points { get; set; }
    public bool PointsCounted { get; set; }
    public int MatchdayRank { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
