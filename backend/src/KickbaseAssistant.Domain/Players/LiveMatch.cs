namespace KickbaseAssistant.Domain.Players;

public enum LiveMatchStatus
{
    NotStarted,
    Live,
    Finished,
}

public class LiveMatch
{
    public required string Id { get; set; }
    public required string CompetitionId { get; set; }
    public int MatchdayNumber { get; set; }
    public DateTimeOffset KickoffAt { get; set; }
    public required string HomeTeamId { get; set; }
    public string? HomeTeamName { get; set; }
    public string? HomeTeamSymbol { get; set; }
    public required string AwayTeamId { get; set; }
    public string? AwayTeamName { get; set; }
    public string? AwayTeamSymbol { get; set; }
    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }
    public LiveMatchStatus Status { get; set; }
    public string? Minute { get; set; }
    public int? HomeTeamPoints { get; set; }
    public int? AwayTeamPoints { get; set; }
    public DateTimeOffset? PointsUpdatedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    public ICollection<LiveMatchPlayer> Players { get; set; } = [];
}
