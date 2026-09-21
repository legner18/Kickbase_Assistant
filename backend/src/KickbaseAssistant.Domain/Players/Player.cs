namespace KickbaseAssistant.Domain.Players;

public class Player
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string TeamId { get; set; }
    public string? TeamName { get; set; }
    public int Position { get; set; }
    public int CompetitionId { get; set; }
    public int MarketValue { get; set; }
    public int TotalPoints { get; set; }
    public bool IsInSquad { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }

    public ICollection<MarketValueSnapshot> MarketValueSnapshots { get; set; } = [];
    public ICollection<LineupPrediction> LineupPredictions { get; set; } = [];
    public ICollection<SellAlert> SellAlerts { get; set; } = [];
}
