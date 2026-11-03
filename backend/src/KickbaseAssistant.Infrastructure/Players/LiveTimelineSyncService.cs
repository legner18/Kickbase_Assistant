using System.Collections.Concurrent;
using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Kickbase;
using KickbaseAssistant.Infrastructure.Kickbase.Dtos;
using KickbaseAssistant.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KickbaseAssistant.Infrastructure.Players;

public class LiveTimelineSyncService(
    KickbaseApiClient kickbaseApiClient,
    LiveMatchStateCache stateCache,
    AppDbContext dbContext,
    ILogger<LiveTimelineSyncService> logger)
{
    private const int MaxLiveFetchesPerRun = 150;
    private const int MaxBackfillFetchesPerRun = 40;
    private const int MaxParallelFetches = 3;
    private static readonly TimeSpan RefreshWindowAfterKickoff = TimeSpan.FromHours(4);
    private static readonly TimeSpan FinalFetchDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan EventTypesRefreshInterval = TimeSpan.FromHours(24);

    private static readonly Dictionary<int, string> MatchFlowTitles = new()
    {
        [10] = "Anpfiff",
        [11] = "Anpfiff 2. Halbzeit",
        [12] = "Halbzeit",
        [13] = "Abpfiff",
        [26] = "Nachspielzeit",
    };

    public async Task<int> SyncAsync(string competitionId, int matchdayNumber, IReadOnlyList<League> leagues, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var leagueIds = leagues.Select(league => league.Id).ToList();
        var ownUserIds = leagues.Select(league => league.OwnUserId).ToList();

        var managedPlayers = await dbContext.LiveManagerPlayers
            .Where(player => player.Score!.MatchdayNumber == matchdayNumber && leagueIds.Contains(player.Score.LeagueId))
            .Select(player => new { player.PlayerId, IsOwn = ownUserIds.Contains(player.Score!.ManagerId) })
            .ToListAsync(ct);
        var managedPlayerIds = managedPlayers.Select(player => player.PlayerId).ToHashSet();
        var ownPlayerIds = managedPlayers.Where(player => player.IsOwn).Select(player => player.PlayerId).ToHashSet();

        var matchPlayers = await dbContext.LiveMatchPlayers
            .Include(player => player.Match)
            .Where(player => player.Match!.CompetitionId == competitionId && player.Match.MatchdayNumber == matchdayNumber)
            .ToListAsync(ct);

        var duePlayers = matchPlayers
            .Where(player => NeedsTimelineRefresh(player, now))
            .OrderByDescending(player => ownPlayerIds.Contains(player.PlayerId))
            .ThenByDescending(player => managedPlayerIds.Contains(player.PlayerId))
            .ThenBy(player => player.TimelineFetchedAt ?? DateTimeOffset.MinValue)
            .ToList();
        var selectedPlayers = duePlayers
            .Where(player => player.Match!.Status != LiveMatchStatus.Finished)
            .Take(MaxLiveFetchesPerRun)
            .Concat(duePlayers
                .Where(player => player.Match!.Status == LiveMatchStatus.Finished)
                .Take(MaxBackfillFetchesPerRun))
            .ToList();
        if (selectedPlayers.Count == 0)
        {
            return 0;
        }

        await EnsureEventTypesAsync(now, ct);
        var playerCenters = await FetchPlayerCentersAsync(competitionId, matchdayNumber, selectedPlayers, ct);

        var selectedRowIds = selectedPlayers.Select(player => player.Id).ToList();
        await dbContext.LiveMatchPlayerEvents
            .Where(matchEvent => selectedRowIds.Contains(matchEvent.LiveMatchPlayerId))
            .LoadAsync(ct);

        var fetchedCount = 0;
        foreach (var player in selectedPlayers)
        {
            if (playerCenters.TryGetValue(player.Id, out var playerCenter) && playerCenter is not null)
            {
                ApplyEvents(player, playerCenter, now);
                fetchedCount++;
            }
            else
            {
                player.TimelinePoints = player.Points;
            }
        }

        return fetchedCount;
    }

    private async Task<IReadOnlyDictionary<long, KickbasePlayerCenterDto?>> FetchPlayerCentersAsync(
        string competitionId, int matchdayNumber, IReadOnlyList<LiveMatchPlayer> players, CancellationToken ct)
    {
        var requests = players.Select(player => (player.Id, player.PlayerId, player.PlayerName)).ToList();
        var results = new ConcurrentDictionary<long, KickbasePlayerCenterDto?>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = MaxParallelFetches, CancellationToken = ct };

        await Parallel.ForEachAsync(requests, options, async (request, token) =>
        {
            try
            {
                results[request.Id] = await kickbaseApiClient.GetPlayerCenterAsync(competitionId, request.PlayerId, matchdayNumber, token);
            }
            catch (Exception ex) when (!KickbaseApiClient.IsBlockedResponse(ex) && !token.IsCancellationRequested)
            {
                results[request.Id] = null;
                logger.LogWarning(ex, "Live-Timeline: Ereignisse für Spieler {PlayerId} ({PlayerName}) konnten nicht geladen werden.", request.PlayerId, request.PlayerName);
            }
        });

        return results;
    }

    private static bool NeedsTimelineRefresh(LiveMatchPlayer player, DateTimeOffset now)
    {
        var match = player.Match!;
        if (player.TimelinePoints is null)
        {
            return true;
        }

        if (player.TimelinePoints != player.Points && now - match.KickoffAt < RefreshWindowAfterKickoff)
        {
            return true;
        }

        return match.FinishedAt is { } finishedAt
            && now - finishedAt >= FinalFetchDelay
            && (player.TimelineFetchedAt is null || player.TimelineFetchedAt < finishedAt + FinalFetchDelay);
    }

    private void ApplyEvents(LiveMatchPlayer player, KickbasePlayerCenterDto playerCenter, DateTimeOffset now)
    {
        var existingEvents = player.Events.ToDictionary(matchEvent => matchEvent.EventId);

        foreach (var kickbaseEvent in playerCenter.Events.DistinctBy(kickbaseEvent => kickbaseEvent.Id))
        {
            var title = ResolveTitle(kickbaseEvent);
            if (kickbaseEvent.Points == 0 && title is null)
            {
                continue;
            }

            if (!existingEvents.Remove(kickbaseEvent.Id, out var row))
            {
                row = new LiveMatchPlayerEvent { EventId = kickbaseEvent.Id };
                player.Events.Add(row);
            }

            row.EventTypeId = kickbaseEvent.EventTypeId;
            row.Title = title;
            row.Points = kickbaseEvent.Points;
            row.Minute = kickbaseEvent.Minute;
        }

        dbContext.LiveMatchPlayerEvents.RemoveRange(existingEvents.Values);
        player.TimelinePoints = playerCenter.Points;
        player.TimelineFetchedAt = now;
    }

    private string? ResolveTitle(KickbasePlayerCenterEventDto kickbaseEvent)
    {
        if (stateCache.EventTypeTitles.TryGetValue(kickbaseEvent.EventTypeId, out var title))
        {
            return title;
        }

        return kickbaseEvent.KeyEvent is { } keyEvent && MatchFlowTitles.TryGetValue(keyEvent, out var matchFlowTitle)
            ? matchFlowTitle
            : null;
    }

    private async Task EnsureEventTypesAsync(DateTimeOffset now, CancellationToken ct)
    {
        if (stateCache.EventTypesLoadedAt is { } loadedAt && now - loadedAt < EventTypesRefreshInterval)
        {
            return;
        }

        try
        {
            var eventTypes = await kickbaseApiClient.GetEventTypesAsync(ct);
            stateCache.EventTypeTitles = eventTypes
                .DistinctBy(eventType => eventType.Id)
                .ToDictionary(eventType => eventType.Id, eventType => eventType.Title);
            stateCache.EventTypesLoadedAt = now;
        }
        catch (Exception ex) when (!KickbaseApiClient.IsBlockedResponse(ex))
        {
            logger.LogWarning(ex, "Live-Timeline: Ereignistypen konnten nicht geladen werden - Titel fehlen vorerst.");
        }
    }
}
