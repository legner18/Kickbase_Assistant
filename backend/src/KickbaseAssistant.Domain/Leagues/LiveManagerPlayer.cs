namespace KickbaseAssistant.Domain.Leagues;

public class LiveManagerPlayer
{
    public long Id { get; set; }
    public long LiveManagerScoreId { get; set; }
    public required string PlayerId { get; set; }
    public required string PlayerName { get; set; }
    public required string TeamId { get; set; }
    public string? MatchId { get; set; }
    public bool InLineup { get; set; }
    public LiveManagerScore? Score { get; set; }
}
