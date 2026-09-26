namespace KickbaseAssistant.Domain.Leagues;

/// <summary>
/// One row per manager per league, upserted (not appended) each LeagueAnalysisJob run - "current
/// best estimate", not a history. EstimatedBudget is derived, not Kickbase's own number (Kickbase
/// only exposes the logged-in user's own exact budget via me/budget) - see LeagueAnalysisJob for
/// the self-calibration formula.
/// </summary>
public class LeagueManagerStat
{
    public long Id { get; set; }
    public required string LeagueId { get; set; }
    public required string ManagerId { get; set; }
    public required string ManagerName { get; set; }
    public double EstimatedBudget { get; set; }
    public double? AverageOverpayPercent { get; set; }
    public int TransferCount { get; set; }
    public int SeasonPoints { get; set; }
    public int MatchdayPoints { get; set; }
    public int SeasonRank { get; set; }
    public int MatchdayRank { get; set; }
    public int CurrentMatchday { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
