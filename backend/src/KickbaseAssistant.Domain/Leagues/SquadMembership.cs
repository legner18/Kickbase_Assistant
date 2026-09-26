using KickbaseAssistant.Domain.Players;

namespace KickbaseAssistant.Domain.Leagues;

/// <summary>
/// Which league a squad player actually belongs to - full-refresh per league, populated by
/// MarketValueSnapshotJob (which already fetches each active league's squad daily). Player.IsInSquad
/// stays a simple global flag (used by jobs that don't care which league), this is what lets the
/// frontend show "my squad in league X" instead of every owned player pooled across all leagues.
/// </summary>
public class SquadMembership
{
    public long Id { get; set; }
    public required string LeagueId { get; set; }
    public required string PlayerId { get; set; }
    public Player? Player { get; set; }
}
