using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Kickbase.Dtos;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Infrastructure.Leagues;

/// <summary>
/// Überträgt einen bereits von Kickbase geladenen eigenen Kader einer Liga in SquadMembership und
/// die Spieler-Stammdaten. Geteilt von MarketValueSnapshotJob (abends, zusätzlich mit Marktwert-
/// Snapshots) und OwnSquadSyncJob (tagsüber alle 2 Minuten), damit beide Schreiber derselben Zeilen
/// exakt dieselbe Logik nutzen.
///
/// Diff statt Drop-and-Rebuild: unveränderte Mitgliedschaften bleiben unangetastet, damit der
/// häufige Lauf ohne Kaderänderung keine Membership-Writes erzeugt und zurückmelden kann, ob sich
/// der Kader überhaupt geändert hat. Speichert nicht selbst - der Aufrufer ruft SaveChangesAsync.
/// </summary>
public class OwnSquadSyncService(AppDbContext dbContext)
{
    /// <returns>true, wenn Spieler dazugekommen oder weggefallen sind.</returns>
    public async Task<bool> ApplyAsync(
        League league,
        string managerName,
        IReadOnlyList<KickbaseSquadPlayerDto> squad,
        IReadOnlyDictionary<string, string> teamNames,
        CancellationToken ct)
    {
        var competitionId = int.TryParse(league.CompetitionId, out var parsed) ? parsed : 0;
        var remoteIds = squad.Select(p => p.Id).ToHashSet();

        var existing = await dbContext.SquadMemberships
            .Where(m => m.LeagueId == league.Id && m.ManagerId == league.OwnUserId)
            .ToListAsync(ct);
        var existingIds = existing.Select(m => m.PlayerId).ToHashSet();

        var removed = existing.Where(m => !remoteIds.Contains(m.PlayerId)).ToList();
        dbContext.SquadMemberships.RemoveRange(removed);
        foreach (var membership in existing.Except(removed))
        {
            membership.ManagerName = managerName;
        }

        var squadChanged = removed.Count > 0;

        foreach (var remotePlayer in squad)
        {
            if (!existingIds.Contains(remotePlayer.Id))
            {
                dbContext.SquadMemberships.Add(new SquadMembership
                {
                    LeagueId = league.Id,
                    ManagerId = league.OwnUserId,
                    ManagerName = managerName,
                    PlayerId = remotePlayer.Id,
                });
                squadChanged = true;
            }

            var player = await dbContext.Players.FindAsync([remotePlayer.Id], ct);
            if (player is null)
            {
                player = new Player
                {
                    Id = remotePlayer.Id,
                    Name = remotePlayer.Name,
                    TeamId = remotePlayer.TeamId,
                    Position = remotePlayer.Position,
                    CompetitionId = competitionId,
                };
                dbContext.Players.Add(player);
            }

            player.Name = remotePlayer.Name;
            player.TeamId = remotePlayer.TeamId;
            player.TeamName = teamNames.GetValueOrDefault(remotePlayer.TeamId, player.TeamName);
            player.Position = remotePlayer.Position;
            player.MarketValue = remotePlayer.MarketValue;
            player.TotalPoints = remotePlayer.TotalPoints;
            player.AveragePoints = remotePlayer.AveragePoints;
            player.StatusCode = remotePlayer.Status;
            player.CompetitionId = competitionId;
            player.IsInSquad = true;
            player.LastSeenAt = DateTimeOffset.UtcNow;
        }

        return squadChanged;
    }
}
