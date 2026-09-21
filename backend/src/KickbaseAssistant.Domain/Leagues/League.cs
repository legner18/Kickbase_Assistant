namespace KickbaseAssistant.Domain.Leagues;

public class League
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string CompetitionId { get; set; }
    public required string OwnUserId { get; set; }
    public bool IsActive { get; set; } = true;
}
