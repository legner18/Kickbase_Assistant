namespace KickbaseAssistant.Domain.Players;

public class MatchOdds
{
    public long Id { get; set; }
    public required string MatchId { get; set; }
    public required string CompetitionId { get; set; }
    public double HomeOdds { get; set; }
    public double DrawOdds { get; set; }
    public double AwayOdds { get; set; }
    public double HomeWinProbability { get; set; }
    public double DrawProbability { get; set; }
    public double AwayWinProbability { get; set; }
    public double ExpectedHomeGoals { get; set; }
    public double ExpectedAwayGoals { get; set; }
    public int BookmakerCount { get; set; }
    public required string SourceHomeTeam { get; set; }
    public required string SourceAwayTeam { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
