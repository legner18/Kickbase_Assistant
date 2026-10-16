using KickbaseAssistant.Infrastructure.Data;

namespace KickbaseAssistant.Infrastructure.Leagues;

public static class OwnSquadQueries
{
    /// <summary>
    /// Spieler-IDs im eigenen Kader - aller aktiven Ligen oder nur der übergebenen. Quelle ist
    /// SquadMembership statt Player.IsInSquad: das Flag ist global über alle Ligen und wurde nie
    /// zurückgesetzt, enthielt also auch Spieler aus archivierten Ligen und längst verkaufte.
    /// </summary>
    public static IQueryable<string> OwnSquadPlayerIds(this AppDbContext dbContext, string? leagueId = null) =>
        dbContext.SquadMemberships
            .Where(m => (leagueId == null || m.LeagueId == leagueId)
                && dbContext.Leagues.Any(l => l.Id == m.LeagueId && l.IsActive && l.OwnUserId == m.ManagerId))
            .Select(m => m.PlayerId)
            .Distinct();
}
