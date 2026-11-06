using System.Collections.Concurrent;
using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Kickbase;
using KickbaseAssistant.Infrastructure.Kickbase.Dtos;
using KickbaseAssistant.Infrastructure.Leagues;
using KickbaseAssistant.Infrastructure.Notifications;
using KickbaseAssistant.Infrastructure.Players;
using KickbaseAssistant.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace KickbaseAssistant.Worker.Jobs;

[DisallowConcurrentExecution]
public class LiveMatchJob(
    KickbaseApiClient kickbaseApiClient,
    LiveHubClient liveHubClient,
    LiveMatchStateCache stateCache,
    LiveLeagueSyncService liveLeagueSyncService,
    LiveTimelineSyncService liveTimelineSyncService,
    LiveNotificationService liveNotificationService,
    AppDbContext dbContext,
    ILogger<LiveMatchJob> logger) : IJob
{
    private const int NotStartedStatus = 0;
    private const int FinishedStatus = 2;
    private const int MaxParallelTeamCenterFetches = 3;
    private static readonly TimeSpan FullSyncInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan FullSyncTolerance = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ScheduleSyncInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan MaxMatchDuration = TimeSpan.FromHours(3);
    private static readonly TimeSpan PostMatchRefreshWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan BackfillWindow = TimeSpan.FromDays(7);
    private static readonly TimeSpan BlockedPause = TimeSpan.FromMinutes(10);

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var now = DateTimeOffset.UtcNow;
        var fullSync = stateCache.FullSyncStartedAt is not { } lastFullSync || now - lastFullSync >= FullSyncInterval - FullSyncTolerance;
        if (fullSync)
        {
            stateCache.FullSyncStartedAt = now;
        }

        if (stateCache.PausedUntil > now)
        {
            if (fullSync)
            {
                await PublishStatusAsync(ct);
            }
            return;
        }

        var activeLeagues = await dbContext.Leagues.Where(league => league.IsActive).ToListAsync(ct);
        var publishStatus = fullSync;

        try
        {
            var succeeded = true;
            foreach (var competitionLeagues in activeLeagues.GroupBy(league => league.CompetitionId))
            {
                succeeded &= await SyncCompetitionAsync(competitionLeagues.Key, [.. competitionLeagues], fullSync, ct);
            }

            if (succeeded)
            {
                stateCache.LastSuccessAt = DateTimeOffset.UtcNow;
            }
        }
        catch (Exception ex) when (KickbaseApiClient.IsBlockedResponse(ex))
        {
            dbContext.ChangeTracker.Clear();
            stateCache.PausedUntil = DateTimeOffset.UtcNow + BlockedPause;
            publishStatus = true;
            logger.LogWarning(ex, "Live-Spiele: Kickbase blockt Anfragen - Live-Polling pausiert bis {PausedUntil:u}.", stateCache.PausedUntil);
        }

        if (publishStatus)
        {
            await PublishStatusAsync(ct);
        }
    }

    private Task PublishStatusAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var pausedUntil = stateCache.PausedUntil > now ? stateCache.PausedUntil : null;
        return liveHubClient.PublishLiveStatusAsync(new LiveWorkerStatus(now, stateCache.LastSuccessAt, pausedUntil), ct);
    }

    private async Task<bool> SyncCompetitionAsync(string competitionId, IReadOnlyList<League> leagues, bool fullSync, CancellationToken ct)
    {
        var succeeded = true;
        if (fullSync)
        {
            try
            {
                await SyncScheduleIfDueAsync(competitionId, ct);
            }
            catch (Exception ex) when (!KickbaseApiClient.IsBlockedResponse(ex))
            {
                dbContext.ChangeTracker.Clear();
                succeeded = false;
                logger.LogWarning(ex, "Live-Spiele: Spielplan für Competition {CompetitionId} konnte nicht geladen werden.", competitionId);
            }
        }

        var refresh = new PointsRefreshResult(0, false);
        try
        {
            refresh = await RefreshRunningMatchesAsync(competitionId, leagues, fullSync, ct);
        }
        catch (Exception ex) when (!KickbaseApiClient.IsBlockedResponse(ex))
        {
            dbContext.ChangeTracker.Clear();
            succeeded = false;
            logger.LogWarning(ex, "Live-Spiele: Punkte für Competition {CompetitionId} konnten nicht aktualisiert werden.", competitionId);
        }

        var refreshedCount = refresh.RefreshedCount;
        var leagueDataChanged = false;
        var now = DateTimeOffset.UtcNow;
        var currentMatchday = fullSync
            ? await dbContext.LiveMatches
                .Where(match => match.CompetitionId == competitionId && match.KickoffAt <= now)
                .MaxAsync(match => (int?)match.MatchdayNumber, ct)
            : null;

        if (currentMatchday is not null)
        {
            foreach (var league in leagues)
            {
                try
                {
                    leagueDataChanged |= await liveLeagueSyncService.SyncAsync(league, currentMatchday.Value, refreshedCount > 0, ct);
                    await dbContext.SaveChangesAsync(ct);
                }
                catch (Exception ex) when (!KickbaseApiClient.IsBlockedResponse(ex))
                {
                    dbContext.ChangeTracker.Clear();
                    succeeded = false;
                    logger.LogWarning(ex, "Live-Spiele: Manager-Punkte für Liga {LeagueName} konnten nicht aktualisiert werden.", league.Name);
                    continue;
                }

                if (refreshedCount > 0)
                {
                    await liveNotificationService.NotifyLeadChangeAsync(league, currentMatchday.Value, ct);
                }
            }

            try
            {
                var timelineCount = await liveTimelineSyncService.SyncAsync(competitionId, currentMatchday.Value, leagues, ct);
                await dbContext.SaveChangesAsync(ct);
                if (timelineCount > 0)
                {
                    leagueDataChanged = true;
                    logger.LogInformation("Live-Timeline: Ereignisse für {Count} Spieler aktualisiert.", timelineCount);
                }
            }
            catch (Exception ex) when (!KickbaseApiClient.IsBlockedResponse(ex))
            {
                dbContext.ChangeTracker.Clear();
                succeeded = false;
                logger.LogWarning(ex, "Live-Timeline: Ereignisse für Competition {CompetitionId} konnten nicht aktualisiert werden.", competitionId);
            }
        }

        if (refresh.Changed || leagueDataChanged)
        {
            await liveHubClient.PublishLiveMatchesUpdateAsync(competitionId, ct);
        }

        return succeeded;
    }

    private readonly record struct PointsRefreshResult(int RefreshedCount, bool Changed);

    private async Task SyncScheduleIfDueAsync(string competitionId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        if (stateCache.ScheduleSyncedAtByCompetition.TryGetValue(competitionId, out var syncedAt)
            && now - syncedAt < ScheduleSyncInterval)
        {
            return;
        }

        var matchdays = await kickbaseApiClient.GetMatchdaysAsync(competitionId, ct);
        var teamNames = await LoadTeamNamesAsync(competitionId, ct);

        var existingMatches = await dbContext.LiveMatches
            .Where(match => match.CompetitionId == competitionId)
            .ToDictionaryAsync(match => match.Id, ct);
        var scheduledMatchIds = new HashSet<string>();

        foreach (var matchday in matchdays)
        {
            foreach (var scheduled in matchday.Matches)
            {
                scheduledMatchIds.Add(scheduled.MatchId);
                if (!existingMatches.TryGetValue(scheduled.MatchId, out var match))
                {
                    match = new LiveMatch
                    {
                        Id = scheduled.MatchId,
                        CompetitionId = competitionId,
                        HomeTeamId = scheduled.HomeTeamId,
                        AwayTeamId = scheduled.AwayTeamId,
                    };
                    dbContext.LiveMatches.Add(match);
                }

                match.MatchdayNumber = matchday.Day;
                match.KickoffAt = scheduled.KickoffAt;
                match.HomeTeamId = scheduled.HomeTeamId;
                match.AwayTeamId = scheduled.AwayTeamId;
                match.HomeTeamSymbol = scheduled.HomeTeamSymbol;
                match.AwayTeamSymbol = scheduled.AwayTeamSymbol;
                match.HomeTeamName = teamNames.TryGetValue(scheduled.HomeTeamId, out var homeName) ? homeName : match.HomeTeamName;
                match.AwayTeamName = teamNames.TryGetValue(scheduled.AwayTeamId, out var awayName) ? awayName : match.AwayTeamName;
                ApplyMatchState(match, scheduled.Status, scheduled.IsLive, scheduled.HomeGoals, scheduled.AwayGoals, scheduled.Minute, now);
            }
        }

        dbContext.LiveMatches.RemoveRange(existingMatches.Values.Where(match => !scheduledMatchIds.Contains(match.Id)));
        await dbContext.SaveChangesAsync(ct);
        stateCache.ScheduleSyncedAtByCompetition[competitionId] = now;
    }

    private async Task<PointsRefreshResult> RefreshRunningMatchesAsync(
        string competitionId, IReadOnlyList<League> leagues, bool fullSync, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var backfillSince = now - BackfillWindow;
        var recentMatches = await dbContext.LiveMatches
            .Where(match => match.CompetitionId == competitionId && match.KickoffAt <= now && match.KickoffAt > backfillSince)
            .ToListAsync(ct);

        var runningMatches = recentMatches.Where(match => NeedsPointsRefresh(match, now, fullSync)).ToList();
        if (runningMatches.Count == 0)
        {
            return new PointsRefreshResult(0, false);
        }

        var runningMatchIds = runningMatches.Select(match => match.Id).ToList();
        await dbContext.LiveMatchPlayers.Where(player => runningMatchIds.Contains(player.MatchId)).LoadAsync(ct);

        var teamCenters = await FetchTeamCentersAsync(competitionId, runningMatches, ct);
        var eventChanges = new List<LivePlayerEventChange>();
        var refreshedCount = 0;
        var changed = false;
        foreach (var match in runningMatches)
        {
            if (!teamCenters.TryGetValue((match.Id, match.HomeTeamId), out var home)
                || !teamCenters.TryGetValue((match.Id, match.AwayTeamId), out var away))
            {
                continue;
            }

            var reportedMatchId = home.Match?.MatchId ?? away.Match?.MatchId;
            if (reportedMatchId is not null && reportedMatchId != match.Id)
            {
                logger.LogWarning(
                    "Live-Spiele: Teamcenter für Spieltag {Matchday} meldet Spiel {ReportedMatchId} statt {MatchId} - übersprungen.",
                    match.MatchdayNumber, reportedMatchId, match.Id);
                continue;
            }

            var state = home.Match ?? away.Match;
            if (state is not null)
            {
                changed |= ApplyMatchState(match, state.Status, state.IsLive, state.HomeGoals, state.AwayGoals, state.Minute, now);
            }

            var changes = match.PointsUpdatedAt is not null ? eventChanges : null;
            changed |= match.HomeTeamPoints != home.Points || match.AwayTeamPoints != away.Points;
            match.HomeTeamName = home.TeamName;
            match.AwayTeamName = away.TeamName;
            match.HomeTeamPoints = home.Points;
            match.AwayTeamPoints = away.Points;
            match.PointsUpdatedAt = now;
            changed |= ApplyPlayers(match, match.HomeTeamId, home.Players, changes);
            changed |= ApplyPlayers(match, match.AwayTeamId, away.Players, changes);
            refreshedCount++;
        }

        await dbContext.SaveChangesAsync(ct);

        if (refreshedCount > 0 && fullSync)
        {
            logger.LogInformation("Live-Spiele: Punkte für {Count} Spiel(e) aktualisiert.", refreshedCount);
        }

        await liveNotificationService.NotifyPlayerEventsAsync(eventChanges, leagues, ct);
        return new PointsRefreshResult(refreshedCount, changed);
    }

    private async Task<IReadOnlyDictionary<(string MatchId, string TeamId), KickbaseTeamCenterDto>> FetchTeamCentersAsync(
        string competitionId, IReadOnlyList<LiveMatch> matches, CancellationToken ct)
    {
        var requests = matches
            .SelectMany(match => new[]
            {
                (MatchId: match.Id, TeamId: match.HomeTeamId, match.MatchdayNumber),
                (MatchId: match.Id, TeamId: match.AwayTeamId, match.MatchdayNumber),
            })
            .ToList();
        var results = new ConcurrentDictionary<(string MatchId, string TeamId), KickbaseTeamCenterDto>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = MaxParallelTeamCenterFetches, CancellationToken = ct };

        await Parallel.ForEachAsync(requests, options, async (request, token) =>
        {
            try
            {
                results[(request.MatchId, request.TeamId)] = await kickbaseApiClient.GetCompetitionTeamCenterAsync(
                    competitionId, request.TeamId, request.MatchdayNumber, token);
            }
            catch (Exception ex) when (!KickbaseApiClient.IsBlockedResponse(ex) && !token.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Live-Spiele: Punkte für Spiel {MatchId} (Team {TeamId}) konnten nicht geladen werden.", request.MatchId, request.TeamId);
            }
        });

        return results;
    }

    private bool ApplyPlayers(
        LiveMatch match, string teamId, IReadOnlyList<KickbaseTeamCenterPlayerDto> players, List<LivePlayerEventChange>? eventChanges)
    {
        var existingRows = match.Players.Where(player => player.TeamId == teamId).ToDictionary(player => player.PlayerId);
        var changed = false;

        foreach (var player in players)
        {
            if (!existingRows.Remove(player.Id, out var row))
            {
                row = new LiveMatchPlayer
                {
                    MatchId = match.Id,
                    TeamId = teamId,
                    PlayerId = player.Id,
                    PlayerName = player.Name,
                };
                match.Players.Add(row);
                changed = true;
            }

            var newEventKeys = FindNewEventKeys(row.EventKeys, player.EventKeys);
            if (eventChanges is not null && newEventKeys.Count > 0)
            {
                eventChanges.Add(new LivePlayerEventChange(match, player.Id, player.Name, newEventKeys, player.Points));
            }

            changed |= row.Points != player.Points || !row.EventKeys.SequenceEqual(player.EventKeys);
            row.PlayerName = player.Name;
            row.Points = player.Points;
            row.EventKeys = [.. player.EventKeys];
        }

        dbContext.LiveMatchPlayers.RemoveRange(existingRows.Values);
        return changed || existingRows.Count > 0;
    }

    private static List<int> FindNewEventKeys(IReadOnlyList<int> previousEventKeys, IReadOnlyList<int> currentEventKeys)
    {
        var remainingPrevious = previousEventKeys
            .GroupBy(eventKey => eventKey)
            .ToDictionary(group => group.Key, group => group.Count());
        var newEventKeys = new List<int>();

        foreach (var eventKey in currentEventKeys)
        {
            if (remainingPrevious.TryGetValue(eventKey, out var count) && count > 0)
            {
                remainingPrevious[eventKey] = count - 1;
            }
            else
            {
                newEventKeys.Add(eventKey);
            }
        }

        return newEventKeys;
    }

    private static bool ApplyMatchState(
        LiveMatch match, int kickbaseStatus, bool isLive, int? homeGoals, int? awayGoals, string? minute, DateTimeOffset now)
    {
        var status = isLive
            ? LiveMatchStatus.Live
            : kickbaseStatus switch
            {
                NotStartedStatus => LiveMatchStatus.NotStarted,
                FinishedStatus => LiveMatchStatus.Finished,
                _ => LiveMatchStatus.Live,
            };

        if (status == LiveMatchStatus.Finished && match.Status == LiveMatchStatus.Live)
        {
            match.FinishedAt = now;
        }

        var changed = match.Status != status || match.HomeGoals != homeGoals || match.AwayGoals != awayGoals || match.Minute != minute;
        match.Status = status;
        match.HomeGoals = homeGoals;
        match.AwayGoals = awayGoals;
        match.Minute = minute;
        return changed;
    }

    private static bool NeedsPointsRefresh(LiveMatch match, DateTimeOffset now, bool fullSync)
    {
        if (match.PointsUpdatedAt is not null && match.Status != LiveMatchStatus.Finished && now - match.KickoffAt < MaxMatchDuration)
        {
            return true;
        }

        return fullSync
            && (match.PointsUpdatedAt is null
                || (match.Status != LiveMatchStatus.Finished && now - match.KickoffAt < MaxMatchDuration)
                || (match.FinishedAt is not null && now - match.FinishedAt < PostMatchRefreshWindow));
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadTeamNamesAsync(string competitionId, CancellationToken ct)
    {
        try
        {
            var table = await kickbaseApiClient.GetCompetitionTableAsync(competitionId, ct);
            return table.ToDictionary(team => team.Id, team => team.Name);
        }
        catch (Exception ex) when (!KickbaseApiClient.IsBlockedResponse(ex))
        {
            logger.LogWarning(ex, "Live-Spiele: Tabelle für Competition {CompetitionId} konnte nicht geladen werden - Teamnamen bleiben unverändert.", competitionId);
            return new Dictionary<string, string>();
        }
    }
}
