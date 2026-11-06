using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Kickbase;
using KickbaseAssistant.Infrastructure.Kickbase.Dtos;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace KickbaseAssistant.Worker.Jobs;

[DisallowConcurrentExecution]
public class BundesligaMarketValueJob(
    KickbaseApiClient kickbaseApiClient,
    AppDbContext dbContext,
    ILogger<BundesligaMarketValueJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var competitionIds = await dbContext.Leagues
            .Where(league => league.IsActive)
            .Select(league => league.CompetitionId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var competitionId in competitionIds)
        {
            try
            {
                await SyncCompetitionAsync(competitionId, today, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Bundesliga-Marktwerte: Competition {CompetitionId} konnte nicht geladen werden.", competitionId);
            }
        }
    }

    private async Task SyncCompetitionAsync(string competitionId, DateOnly today, CancellationToken ct)
    {
        var teams = await kickbaseApiClient.GetCompetitionTableAsync(competitionId, ct);
        var remotePlayers = new List<(KickbaseTeamProfilePlayerDto Player, KickbaseTeamDto Team)>();

        foreach (var team in teams)
        {
            try
            {
                var profile = await kickbaseApiClient.GetTeamProfileAsync(competitionId, team.Id, ct);
                if (profile.Players.Count == 0)
                {
                    logger.LogWarning(
                        "Bundesliga-Marktwerte: teamprofile für {TeamName} ohne lesbare Spieler, Felder der Antwort: {Fields}",
                        team.Name, string.Join(", ", profile.TopLevelFields));
                    continue;
                }

                remotePlayers.AddRange(profile.Players.Select(player => (player, team)));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Bundesliga-Marktwerte: teamprofile für {TeamName} konnte nicht geladen werden.", team.Name);
            }
        }

        if (remotePlayers.Count == 0)
        {
            return;
        }

        var playerIds = remotePlayers.Select(remote => remote.Player.Id).Distinct().ToList();
        var existingPlayers = await dbContext.Players
            .Where(player => playerIds.Contains(player.Id))
            .ToDictionaryAsync(player => player.Id, ct);
        var snapshottedToday = (await dbContext.MarketValueSnapshots
            .Where(snapshot => playerIds.Contains(snapshot.PlayerId) && snapshot.Date == today)
            .Select(snapshot => snapshot.PlayerId)
            .ToListAsync(ct)).ToHashSet();
        var previousValues = await dbContext.MarketValueSnapshots
            .Where(snapshot => playerIds.Contains(snapshot.PlayerId) && snapshot.Date < today)
            .GroupBy(snapshot => snapshot.PlayerId)
            .Select(playerSnapshots => new
            {
                PlayerId = playerSnapshots.Key,
                playerSnapshots.OrderByDescending(snapshot => snapshot.Date).First().MarketValue,
            })
            .ToDictionaryAsync(previous => previous.PlayerId, previous => previous.MarketValue, ct);

        var competitionNumber = int.TryParse(competitionId, out var parsed) ? parsed : 0;
        var now = DateTimeOffset.UtcNow;
        var snapshotsCreated = 0;

        foreach (var (remotePlayer, team) in remotePlayers.DistinctBy(remote => remote.Player.Id))
        {
            if (!existingPlayers.TryGetValue(remotePlayer.Id, out var player))
            {
                player = new Player
                {
                    Id = remotePlayer.Id,
                    Name = remotePlayer.Name,
                    TeamId = team.Id,
                    Position = remotePlayer.Position,
                    CompetitionId = competitionNumber,
                };
                dbContext.Players.Add(player);
            }

            player.Name = remotePlayer.Name;
            player.TeamId = team.Id;
            player.TeamName = team.Name;
            player.Position = remotePlayer.Position;
            player.MarketValue = remotePlayer.MarketValue;
            player.AveragePoints = remotePlayer.AveragePoints ?? player.AveragePoints;
            player.TotalPoints = remotePlayer.TotalPoints ?? player.TotalPoints;
            player.StatusCode = remotePlayer.Status ?? player.StatusCode;
            player.CompetitionId = competitionNumber;
            player.LastSeenAt = now;

            if (snapshottedToday.Contains(remotePlayer.Id))
            {
                continue;
            }

            dbContext.MarketValueSnapshots.Add(new MarketValueSnapshot
            {
                PlayerId = remotePlayer.Id,
                Date = today,
                MarketValue = remotePlayer.MarketValue,
                MarketValueChange = previousValues.TryGetValue(remotePlayer.Id, out var previousValue)
                    ? remotePlayer.MarketValue - previousValue
                    : 0,
                StatusCode = remotePlayer.Status ?? player.StatusCode,
            });
            snapshotsCreated++;
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Bundesliga-Marktwerte fertig: Competition {CompetitionId}, {PlayerCount} Spieler, {SnapshotCount} neue Snapshots.",
            competitionId, playerIds.Count, snapshotsCreated);
    }
}
