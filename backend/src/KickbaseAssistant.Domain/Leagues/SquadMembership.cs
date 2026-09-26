using KickbaseAssistant.Domain.Players;

namespace KickbaseAssistant.Domain.Leagues;

/// <summary>
/// Which manager's squad a player belongs to, in a given league - full-refresh per (league,
/// manager), so it's fine to drop and recompletely rebuild rather than tracking history. The
/// user's own rows are written by MarketValueSnapshotJob (which already fetches their squad
/// daily for market-value tracking); every other manager's rows are written by
/// LeagueSquadSyncJob. Together this is what lets the frontend show "whose squad is this player
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
