using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Kickbase;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace KickbaseAssistant.Worker.Jobs;

/// <summary>
/// Phase 2 (Marktwert-Tracker): syncs the user's leagues and their squads, then stores one
/// MarketValueSnapshot per player per day. Runs once daily, after Kickbase's own market value
/// update (community knowledge: values typically update in the evening, CET).
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
        var playersUpdated = 0;
        var snapshotsCreated = 0;

        foreach (var league in activeLeagues)
        {
            var squad = await kickbaseApiClient.GetSquadAsync(league.Id, ct);
            var competitionId = int.TryParse(league.CompetitionId, out var parsed) ? parsed : 0;

            foreach (var remotePlayer in squad)
            {
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
