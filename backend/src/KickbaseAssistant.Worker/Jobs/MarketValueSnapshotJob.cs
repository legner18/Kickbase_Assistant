using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Kickbase;
using KickbaseAssistant.Infrastructure.Kickbase.Dtos;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace KickbaseAssistant.Worker.Jobs;

/// <summary>
/// Phase 2 (Marktwert-Tracker): syncs the user's leagues and their squads, then stores one
/// MarketValueSnapshot per player per day. Runs once daily, after Kickbase's own market value
/// update (community knowledge: values typically update in the evening, CET).
///
/// Also owns SquadMembership (full-refresh per league each run) - the only place that records
/// *which* league a squad player actually belongs to, since Player.IsInSquad itself is a single
/// global flag shared across all leagues a player happens to be owned in.
/// </summary>
public class MarketValueSnapshotJob(
    KickbaseApiClient kickbaseApiClient,
    AppDbContext dbContext,
    ILogger<MarketValueSnapshotJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var me = await kickbaseApiClient.GetMeAsync(ct);
        var remoteLeagues = await kickbaseApiClient.GetLeaguesAsync(ct);

        foreach (var remoteLeague in remoteLeagues)
        {
            var league = await dbContext.Leagues.FindAsync([remoteLeague.Id], ct);
            if (league is null)
            {
                league = new League
                {
                    Id = remoteLeague.Id,
                    Name = remoteLeague.Name,
                    CompetitionId = remoteLeague.CompetitionId,
                    OwnUserId = me.Id,
                };
                dbContext.Leagues.Add(league);
            }
            else
            {
                league.Name = remoteLeague.Name;
                league.CompetitionId = remoteLeague.CompetitionId;
            }
        }

        await dbContext.SaveChangesAsync(ct);

        var activeLeagues = await dbContext.Leagues.Where(l => l.IsActive).ToListAsync(ct);
        var snapshottedPlayerIds = new HashSet<string>();
        var teamNamesByCompetition = new Dictionary<string, Dictionary<string, string>>();
        var playersUpdated = 0;
        var snapshotsCreated = 0;

        foreach (var league in activeLeagues)
        {
            IReadOnlyList<KickbaseSquadPlayerDto> squad;
            try
            {
                squad = await kickbaseApiClient.GetSquadAsync(league.Id, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Marktwert-Snapshot: Kader für Liga {LeagueName} konnte nicht geladen werden - übersprungen.", league.Name);
                continue;
            }

            var competitionId = int.TryParse(league.CompetitionId, out var parsed) ? parsed : 0;

            if (!teamNamesByCompetition.TryGetValue(league.CompetitionId, out var teamNames))
            {
                try
                {
                    var table = await kickbaseApiClient.GetCompetitionTableAsync(league.CompetitionId, ct);
                    teamNames = table.ToDictionary(t => t.Id, t => t.Name);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Marktwert-Snapshot: Tabelle für Competition {CompetitionId} konnte nicht geladen werden - Teamnamen bleiben unverändert.", league.CompetitionId);
                    teamNames = [];
                }

                teamNamesByCompetition[league.CompetitionId] = teamNames;
            }

            var oldMemberships = await dbContext.SquadMemberships
                .Where(m => m.LeagueId == league.Id && m.ManagerId == league.OwnUserId)
                .ToListAsync(ct);
            dbContext.SquadMemberships.RemoveRange(oldMemberships);

            foreach (var remotePlayer in squad)
            {
                dbContext.SquadMemberships.Add(new SquadMembership
                {
                    LeagueId = league.Id,
                    ManagerId = league.OwnUserId,
                    ManagerName = me.Name,
                    PlayerId = remotePlayer.Id,
                });

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
                player.CompetitionId = competitionId;
                player.IsInSquad = true;
                player.LastSeenAt = DateTimeOffset.UtcNow;
                playersUpdated++;

                if (!snapshottedPlayerIds.Add(remotePlayer.Id))
                {
                    continue;
                }

                var alreadySnapshotted = await dbContext.MarketValueSnapshots
                    .AnyAsync(s => s.PlayerId == remotePlayer.Id && s.Date == today, ct);
                if (alreadySnapshotted)
                {
                    continue;
                }

                var previous = await dbContext.MarketValueSnapshots
                    .Where(s => s.PlayerId == remotePlayer.Id && s.Date < today)
                    .OrderByDescending(s => s.Date)
                    .FirstOrDefaultAsync(ct);

                dbContext.MarketValueSnapshots.Add(new MarketValueSnapshot
                {
                    PlayerId = remotePlayer.Id,
                    Date = today,
                    MarketValue = remotePlayer.MarketValue,
                    MarketValueChange = remotePlayer.MarketValue - (previous?.MarketValue ?? remotePlayer.MarketValue),
                });
                snapshotsCreated++;
            }
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Marktwert-Snapshot fertig: {LeagueCount} Ligen, {PlayerCount} Spieler aktualisiert, {SnapshotCount} neue Snapshots.",
            activeLeagues.Count, playersUpdated, snapshotsCreated);
    }
}
