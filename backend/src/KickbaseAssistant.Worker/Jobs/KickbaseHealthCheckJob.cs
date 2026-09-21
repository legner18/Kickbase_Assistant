using KickbaseAssistant.Infrastructure.Kickbase;
using Quartz;

namespace KickbaseAssistant.Worker.Jobs;

/// <summary>
/// Verifies periodically that login + token refresh against the Kickbase API still works.
/// Serves as the template for the Marktwert-Tracker / LigaInsider jobs added in later phases.
/// </summary>
public class KickbaseHealthCheckJob(
    KickbaseApiClient kickbaseApiClient,
    ILogger<KickbaseHealthCheckJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var me = await kickbaseApiClient.GetMeAsync(context.CancellationToken);
            logger.LogInformation("Kickbase-Healthcheck OK. Eingeloggt als {Name} ({Id}).", me.Name, me.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Kickbase-Healthcheck fehlgeschlagen");
        }
    }
}
