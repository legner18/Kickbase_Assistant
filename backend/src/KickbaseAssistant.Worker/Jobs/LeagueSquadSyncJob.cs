using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Kickbase;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace KickbaseAssistant.Worker.Jobs;

/// <summary>
/// Gives the frontend visibility into every league-mate's squad, not just the user's own
/// (MarketValueSnapshotJob already owns that - see its notes). Full-refresh per (league,
/// manager) each run, same pattern as MarketScanResult.
///
/// Also backfills MarketValueSnapshot history for any newly-seen player using Kickbase's 92-day
/// history endpoint, once, so a league-mate's player has a real value chart from day one instead
/// of waiting for them to happen to pass through our own squad or the market scanner. This does
/// NOT keep such players' history current daily the way the user's own squad is tracked - it's a
/// one-time seed, not an ongoing subscription (only players in the user's own squad get fresh
/// daily snapshots).
/// </summary>
public class LeagueSquadSyncJob(
    KickbaseApiClient kickbaseApiClient,
    AppDbContext dbContext,
    ILogger<LeagueSquadSyncJob> logger) : IJob
{
    private const int MarketValueTimeframeDays = 92;

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var activeLeagues = await dbContext.Leagues.Where(l => l.IsActive).ToListAsync(ct);
        var teamNamesByCompetition = new Dictionary<string, Dictionary<string, string>>();
        var managersProcessed = 0;
        var playersBackfilled = 0;

        foreach (var league in activeLeagues)
        {
            IReadOnlyList<(string UserId, string Name)> managers;
            try
            {
                var ranking = await kickbaseApiClient.GetRankingAsync(league.Id, ct);
                managers = ranking.Users.Select(u => (u.UserId, u.Name)).ToList();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Liga-Kader-Sync: Ranking für {LeagueName} konnte nicht geladen werden.", league.Name);
                continue;
            }

            if (!teamNamesByCompetition.TryGetValue(league.CompetitionId, out var teamNames))
            {
                try
                {
                    var table = await kickbaseApiClient.GetCompetitionTableAsync(league.CompetitionId, ct);
                    teamNames = table.ToDictionary(t => t.Id, t => t.Name);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Liga-Kader-Sync: Tabelle für Competition {CompetitionId} konnte nicht geladen werden.", league.CompetitionId);
                    teamNames = [];
                }

                teamNamesByCompetition[league.CompetitionId] = teamNames;
            }

            foreach (var (managerId, managerName) in managers)
            {
                if (managerId == league.OwnUserId)
                {
                    continue; // eigener Kader: siehe MarketValueSnapshotJob, bleibt unangetastet
                }

                try
                {
                    await SyncManagerSquadAsync(league, managerId, managerName, teamNames, ct);
                    managersProcessed++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Liga-Kader-Sync: Kader von {ManagerName} in {LeagueName} konnte nicht geladen werden.", managerName, league.Name);
                }
            }
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Liga-Kader-Sync fertig: {LeagueCount} Ligen, {ManagerCount} fremde Käder synchronisiert, {BackfillCount} Spieler mit Historie befüllt.",
            activeLeagues.Count, managersProcessed, playersBackfilled);

        async Task SyncManagerSquadAsync(
            League league, string managerId, string managerName, Dictionary<string, string> teamNames, CancellationToken ct)
        {
            var squad = await kickbaseApiClient.GetManagerSquadAsync(league.Id, managerId, ct);
            var competitionId = int.TryParse(league.CompetitionId, out var parsed) ? parsed : 0;

            var oldMemberships = await dbContext.SquadMemberships
                .Where(m => m.LeagueId == league.Id && m.ManagerId == managerId)
                .ToListAsync(ct);
            dbContext.SquadMemberships.RemoveRange(oldMemberships);

            foreach (var remotePlayer in squad)
            {
                dbContext.SquadMemberships.Add(new SquadMembership
                {
                    LeagueId = league.Id,
                    ManagerId = managerId,
                    ManagerName = managerName,
                    PlayerId = remotePlayer.PlayerId,
                });

                var player = await dbContext.Players.FindAsync([remotePlayer.PlayerId], ct);
                if (player is null)
                {
                    player = new Player
                    {
                        Id = remotePlayer.PlayerId,
                        Name = remotePlayer.PlayerName,
                        TeamId = remotePlayer.TeamId,
                        Position = remotePlayer.Position,
                        CompetitionId = competitionId,
                    };
                    dbContext.Players.Add(player);
                }

                player.Name = remotePlayer.PlayerName;
                player.TeamId = remotePlayer.TeamId;
                player.TeamName = teamNames.GetValueOrDefault(remotePlayer.TeamId, player.TeamName);
                player.Position = remotePlayer.Position;
                player.MarketValue = remotePlayer.MarketValue;
                player.TotalPoints = remotePlayer.TotalPoints;
                player.AveragePoints = remotePlayer.AveragePoints;
                player.CompetitionId = competitionId;
                player.LastSeenAt = DateTimeOffset.UtcNow;

                var hasHistory = await dbContext.MarketValueSnapshots.AnyAsync(s => s.PlayerId == remotePlayer.PlayerId, ct);
                if (!hasHistory)
                {
                    await BackfillHistoryAsync(league.CompetitionId, remotePlayer.PlayerId, ct);
                    playersBackfilled++;
                }
            }
        }

        async Task BackfillHistoryAsync(string competitionId, string playerId, CancellationToken ct)
        {
            try
            {
                var history = await kickbaseApiClient.GetMarketValueHistoryAsync(competitionId, playerId, MarketValueTimeframeDays, ct);
                var epoch = DateOnly.FromDateTime(DateTime.UnixEpoch);
                double? previousValue = null;

                foreach (var point in history.OrderBy(p => p.DayIndex))
                {
                    var date = epoch.AddDays((int)point.DayIndex);
                    dbContext.MarketValueSnapshots.Add(new MarketValueSnapshot
                    {
                        PlayerId = playerId,
                        Date = date,
                        MarketValue = (int)point.MarketValue,
                        MarketValueChange = (int)(point.MarketValue - (previousValue ?? point.MarketValue)),
                    });
                    previousValue = point.MarketValue;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Liga-Kader-Sync: Marktwert-Historie für Spieler {PlayerId} konnte nicht befüllt werden.", playerId);
            }
        }
    }
}
