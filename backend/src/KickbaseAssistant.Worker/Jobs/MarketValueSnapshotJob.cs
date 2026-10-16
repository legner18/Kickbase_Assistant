using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Kickbase;
using KickbaseAssistant.Infrastructure.Kickbase.Dtos;
using KickbaseAssistant.Infrastructure.Leagues;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace KickbaseAssistant.Worker.Jobs;

/// <summary>
/// Phase 2 (Marktwert-Tracker): syncs the user's leagues and their squads, then stores one
/// MarketValueSnapshot per player per day. Trigger fires every minute from 22:01 to 22:45 (see
/// Program.cs) - Kickbase's own market value update runs at 22:00, and a single fixed-time run
/// would regularly race it and lock in a false "no change" snapshot. A player is only snapshotted
/// once the fetched market value actually differs from the last stored value, or once the window's
/// last run (22:45) is reached - whichever comes first. That also means later runs within the same
/// window are cheap no-ops for players already snapshotted today.
///
/// Also refreshes the user's own SquadMembership rows (via OwnSquadSyncService, shared with the
/// daytime OwnSquadSyncJob) - the only place that records *which* league a squad player actually
/// belongs to, since Player.IsInSquad itself is a single global flag shared across all leagues a
/// player happens to be owned in.
/// </summary>
public class MarketValueSnapshotJob(
    KickbaseApiClient kickbaseApiClient,
    LeagueSyncService leagueSyncService,
    OwnSquadSyncService ownSquadSyncService,
    AppDbContext dbContext,
    ILogger<MarketValueSnapshotJob> logger) : IJob
{
    private static readonly TimeZoneInfo GermanyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private const int SnapshotWindowCutoffHour = 22;
    private const int SnapshotWindowCutoffMinute = 45;

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var isLastRunOfWindow = IsAtOrPastCutoff(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, GermanyTimeZone));

        await leagueSyncService.SyncAsync(ct);

        var me = await kickbaseApiClient.GetMeAsync(ct);
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

            await ownSquadSyncService.ApplyAsync(league, me.Name, squad, teamNames, ct);
            playersUpdated += squad.Count;

            foreach (var remotePlayer in squad)
            {
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

                var hasActuallyChanged = previous is not null && previous.MarketValue != remotePlayer.MarketValue;
                if (!hasActuallyChanged && !isLastRunOfWindow)
                {
                    // Kickbase hat den Wert für diesen Spieler vermutlich noch nicht aktualisiert -
                    // beim nächsten Minuten-Tick innerhalb des Fensters erneut versuchen, statt
                    // einen verfrühten "0 Änderung"-Snapshot festzuschreiben.
                    continue;
                }

                dbContext.MarketValueSnapshots.Add(new MarketValueSnapshot
                {
                    PlayerId = remotePlayer.Id,
                    Date = today,
                    MarketValue = remotePlayer.MarketValue,
                    MarketValueChange = remotePlayer.MarketValue - (previous?.MarketValue ?? remotePlayer.MarketValue),
                    StatusCode = remotePlayer.Status,
                });
                snapshotsCreated++;
            }
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Marktwert-Snapshot fertig: {LeagueCount} Ligen, {PlayerCount} Spieler aktualisiert, {SnapshotCount} neue Snapshots.",
            activeLeagues.Count, playersUpdated, snapshotsCreated);
    }

    private static bool IsAtOrPastCutoff(DateTimeOffset germanyLocalTime) =>
        germanyLocalTime.Hour > SnapshotWindowCutoffHour
        || (germanyLocalTime.Hour == SnapshotWindowCutoffHour && germanyLocalTime.Minute >= SnapshotWindowCutoffMinute);
}
