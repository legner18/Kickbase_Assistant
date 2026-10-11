using KickbaseAssistant.Infrastructure.Leagues;
using Quartz;

namespace KickbaseAssistant.Worker.Jobs;

/// <summary>
/// Umbauplan Phase 1 (Liga-Sync): stündlicher, eigenständiger Lauf von LeagueSyncService, damit
/// neue Ligen und verlassene (zu archivierende) Ligen zügig erkannt werden, statt nur einmal
/// täglich als Nebeneffekt von MarketValueSnapshotJob.
/// </summary>
public class LeagueSyncJob(LeagueSyncService leagueSyncService, ILogger<LeagueSyncJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var result = await leagueSyncService.SyncAsync(context.CancellationToken);

        if (result.Created > 0 || result.Archived > 0)
        {
            logger.LogInformation(
                "Liga-Sync: {Created} neue Liga(en), {Updated} aktualisiert, {Archived} archiviert.",
                result.Created, result.Updated, result.Archived);
        }
    }
}
