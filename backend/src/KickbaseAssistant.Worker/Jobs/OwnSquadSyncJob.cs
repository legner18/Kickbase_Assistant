using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Kickbase;
using KickbaseAssistant.Infrastructure.Kickbase.Dtos;
using KickbaseAssistant.Infrastructure.Leagues;
using KickbaseAssistant.Infrastructure.Players;
using KickbaseAssistant.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace KickbaseAssistant.Worker.Jobs;

/// <summary>
/// Hält den eigenen Kader und den Kontostand tagsüber aktuell, damit Verkaufsplaner, Kader und
/// Dashboard nach einem Kauf/Verkauf nicht bis zum abendlichen MarketValueSnapshotJob (22:01-22:45)
/// veraltete Spieler und einen veralteten Kontostand zeigen. Läuft alle 2 Minuten und macht pro Liga
/// nur zwei leichte Aufrufe (Kader + Budget); die Bundesliga-Tabelle wird nur geladen, wenn ein
/// Spieler noch keinen Teamnamen hat (z.B. frisch gekauft und vorher nie gesehen).
///
/// Pausiert nachts (1-7 Uhr, wie LiveMarketJob) und rund um das Snapshot-Fenster (22:00-22:49),
/// weil MarketValueSnapshotJob dort minütlich dieselben SquadMembership-Zeilen schreibt.
///
/// Kontostand: aktualisiert nur Balance/Status/RosterGaps einer bereits vorhandenen
/// LeagueBalanceStatus-Zeile - sonst würde der Verkaufsplaner nach einem Verkauf den neuen Kader
/// mit dem alten Kontostand verrechnen. Kreditrahmen und Spieltagsinfos bleiben Sache des
/// stündlichen BalanceGuardJob (brauchen Ranking + Spielplan), der die Zeile auch anlegt.
///
/// Pusht "SquadUpdated" per SignalR nur, wenn sich Kader oder Kontostand tatsächlich geändert
/// haben - der Verkaufsplaner lädt daraufhin selbst neu.
/// </summary>
[DisallowConcurrentExecution]
public class OwnSquadSyncJob(
    KickbaseApiClient kickbaseApiClient,
    OwnSquadSyncService ownSquadSyncService,
    PlayerMetricsService playerMetricsService,
    LiveHubClient liveHubClient,
    AppDbContext dbContext,
    ILogger<OwnSquadSyncJob> logger) : IJob
{
    private static readonly TimeZoneInfo GermanyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        if (IsPaused(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, GermanyTimeZone)))
        {
            return;
        }

        var activeLeagues = await dbContext.Leagues.Where(l => l.IsActive).ToListAsync(ct);
        if (activeLeagues.Count == 0)
        {
            return;
        }

        var me = await kickbaseApiClient.GetMeAsync(ct);
        var changedLeagueIds = new List<string>();

        foreach (var league in activeLeagues)
        {
            try
            {
                if (await SyncLeagueAsync(league, me.Name, ct))
                {
                    changedLeagueIds.Add(league.Id);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Kader-Sync: Liga {LeagueName} konnte nicht synchronisiert werden.", league.Name);
            }
        }

        await dbContext.SaveChangesAsync(ct);

        foreach (var leagueId in changedLeagueIds)
        {
            await liveHubClient.PublishSquadUpdateAsync(leagueId, ct);
        }

        if (changedLeagueIds.Count > 0)
        {
            logger.LogInformation("Kader-Sync: Änderungen in {Count} Liga(en) übernommen.", changedLeagueIds.Count);
        }
    }

    private async Task<bool> SyncLeagueAsync(League league, string managerName, CancellationToken ct)
    {
        // Erst alle Kickbase-Aufrufe, dann die DB - schlägt ein Aufruf fehl, bleibt für diese Liga
        // nichts halb geändert im Change-Tracker liegen.
        var squad = await kickbaseApiClient.GetSquadAsync(league.Id, ct);
        var budget = await kickbaseApiClient.GetMyBudgetAsync(league.Id, ct);
        var teamNames = await LoadTeamNamesIfNeededAsync(league.CompetitionId, squad, ct);

        var squadChanged = await ownSquadSyncService.ApplyAsync(league, managerName, squad, teamNames, ct);

        var statusRow = await dbContext.LeagueBalanceStatuses.FirstOrDefaultAsync(s => s.LeagueId == league.Id, ct);
        var balanceChanged = statusRow is not null && statusRow.Balance != budget.Budget;
        if (statusRow is not null && (squadChanged || balanceChanged))
        {
            var positiveBuffer = await playerMetricsService.ComputePositiveMarketValueBufferAsync(squad.Select(p => p.Id), ct);
            statusRow.Balance = budget.Budget;
            statusRow.Status = BalanceGuardCalculator.GetStatus(budget.Budget, positiveBuffer);
            statusRow.RosterGaps = [.. RosterCheck.FindGaps(squad.Select(p => p.Position).ToList())];
            statusRow.UpdatedAt = DateTimeOffset.UtcNow;
        }

        return squadChanged || balanceChanged;
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadTeamNamesIfNeededAsync(
        string competitionId, IReadOnlyList<KickbaseSquadPlayerDto> squad, CancellationToken ct)
    {
        var squadIds = squad.Select(p => p.Id).ToList();
        var knownTeamIds = await dbContext.Players
            .Where(p => squadIds.Contains(p.Id) && p.TeamName != null)
            .Select(p => new { p.Id, p.TeamId })
            .ToDictionaryAsync(p => p.Id, p => p.TeamId, ct);

        var needsTeamNames = squad.Any(p => !knownTeamIds.TryGetValue(p.Id, out var teamId) || teamId != p.TeamId);
        if (!needsTeamNames)
        {
            return new Dictionary<string, string>();
        }

        try
        {
            var table = await kickbaseApiClient.GetCompetitionTableAsync(competitionId, ct);
            return table.ToDictionary(t => t.Id, t => t.Name);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Kader-Sync: Tabelle für Competition {CompetitionId} konnte nicht geladen werden - Teamnamen bleiben unverändert.", competitionId);
            return new Dictionary<string, string>();
        }
    }

    private static bool IsPaused(DateTimeOffset germanyLocalTime) =>
        germanyLocalTime.Hour is >= 1 and < 7
        || (germanyLocalTime.Hour == 22 && germanyLocalTime.Minute < 50);
}
