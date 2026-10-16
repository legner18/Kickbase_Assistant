using KickbaseAssistant.Domain.Players;

namespace KickbaseAssistant.Domain.Leagues;

/// <summary>
/// Which manager's squad a player belongs to, in a given league - no history, just the current
/// state per (league, manager). The user's own rows are written by OwnSquadSyncService, called
/// every 2 minutes during the day by OwnSquadSyncJob and in the evening by MarketValueSnapshotJob;
/// every other manager's rows are written by LeagueSquadSyncJob. Together this is what lets the frontend show "whose squad is this player
/// in" for any manager in the league, not just the user's own.
/// </summary>
public class SquadMembership
{
    public long Id { get; set; }
    public required string LeagueId { get; set; }
    public required string ManagerId { get; set; }
    public required string ManagerName { get; set; }
    public required string PlayerId { get; set; }
    public Player? Player { get; set; }
}
