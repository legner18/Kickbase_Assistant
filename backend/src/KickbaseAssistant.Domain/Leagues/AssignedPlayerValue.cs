namespace KickbaseAssistant.Domain.Leagues;

public class AssignedPlayerValue
{
    public long Id { get; set; }
    public required string LeagueId { get; set; }
    public required string ManagerId { get; set; }
    public required string PlayerId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public double MarketValue { get; set; }
}
