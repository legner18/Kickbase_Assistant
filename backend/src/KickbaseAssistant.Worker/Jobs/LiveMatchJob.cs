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

[DisallowConcurrentExecution]
public class LiveMatchJob(
    KickbaseApiClient kickbaseApiClient,
    LiveHubClient liveHubClient,
    LiveMatchStateCache stateCache,
    LiveLeagueSyncService liveLeagueSyncService,
    LiveTimelineSyncService liveTimelineSyncService,
    AppDbContext dbContext,
    ILogger<LiveMatchJob> logger) : IJob
{
    private const int NotStartedStatus = 0;
    private const int FinishedStatus = 2;
    private static readonly TimeSpan ScheduleSyncInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan MaxMatchDuration = TimeSpan.FromHours(3);
    private static readonly TimeSpan PostMatchRefreshWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan BackfillWindow = TimeSpan.FromDays(7);
    private static readonly TimeSpan BlockedPause = TimeSpan.FromMinutes(10);

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        if (stateCache.PausedUntil > DateTimeOffset.UtcNow)
        {
            return;
        }

        var activeLeagues = await dbContext.Leagues.Where(league => league.IsActive).ToListAsync(ct);

        try
        {
            foreach (var competitionLeagues in activeLeagues.GroupBy(league => league.CompetitionId))
            {
                await SyncCompetitionAsync(competitionLeagues.Key, [.. competitionLeagues], ct);
            }
        }
        catch (Exception ex) when (KickbaseApiClient.IsBlockedResponse(ex))
        {
            dbContext.ChangeTracker.Clear();
            stateCache.PausedUntil = DateTimeOffset.UtcNow + BlockedPause;
            logger.LogWarning(ex, "Live-Spiele: Kickbase blockt Anfragen - Live-Polling pausiert bis {PausedUntil:u}.", stateCache.PausedUntil);
        }
    }

    private async Task SyncCompetitionAsync(string competitionId, IReadOnlyList<League> leagues, CancellationToken ct)
    {
        try
        {
            await SyncScheduleIfDueAsync(competitionId, ct);
        }
        catch (Exception ex) when (!KickbaseApiClient.IsBlockedResponse(ex))
        {
            dbContext.ChangeTracker.Clear();
            logger.LogWarning(ex, "Live-Spiele: Spielplan für Competition {CompetitionId} konnte nicht geladen werden.", competitionId);
        }

        var refreshedCount = 0;
        try
        {
            refreshedCount = await RefreshRunningMatchesAsync(competitionId, ct);
        }
        catch (Exception ex) when (!KickbaseApiClient.IsBlockedResponse(ex))
        {
            dbContext.ChangeTracker.Clear();
            logger.LogWarning(ex, "Live-Spiele: Punkte für Competition {CompetitionId} konnten nicht aktualisiert werden.", competitionId);
        }

        var now = DateTimeOffset.UtcNow;
        var currentMatchday = await dbContext.LiveMatches
            .Where(match => match.CompetitionId == competitionId && match.KickoffAt <= now)
            .MaxAsync(match => (int?)match.MatchdayNumber, ct);

        var leagueDataChanged = false;
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
                    logger.LogWarning(ex, "Live-Spiele: Manager-Punkte für Liga {LeagueName} konnten nicht aktualisiert werden.", league.Name);
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
                logger.LogWarning(ex, "Live-Timeline: Ereignisse für Competition {CompetitionId} konnten nicht aktualisiert werden.", competitionId);
            }
        }

        if (refreshedCount > 0 || leagueDataChanged)
        {
            await liveHubClient.PublishLiveMatchesUpdateAsync(competitionId, ct);
        }
    }

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

    private async Task<int> RefreshRunningMatchesAsync(string competitionId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var backfillSince = now - BackfillWindow;
        var recentMatches = await dbContext.LiveMatches
            .Where(match => match.CompetitionId == competitionId && match.KickoffAt <= now && match.KickoffAt > backfillSince)
            .ToListAsync(ct);

        var runningMatches = recentMatches.Where(match => NeedsPointsRefresh(match, now)).ToList();
        if (runningMatches.Count == 0)
        {
            return 0;
        }

        var runningMatchIds = runningMatches.Select(match => match.Id).ToList();
        await dbContext.LiveMatchPlayers.Where(player => runningMatchIds.Contains(player.MatchId)).LoadAsync(ct);

        var refreshedCount = 0;
        foreach (var match in runningMatches)
        {
            KickbaseTeamCenterDto home;
            KickbaseTeamCenterDto away;
            try
            {
                home = await kickbaseApiClient.GetCompetitionTeamCenterAsync(competitionId, match.HomeTeamId, match.MatchdayNumber, ct);
                away = await kickbaseApiClient.GetCompetitionTeamCenterAsync(competitionId, match.AwayTeamId, match.MatchdayNumber, ct);
            }
            catch (Exception ex) when (!KickbaseApiClient.IsBlockedResponse(ex))
            {
                logger.LogWarning(ex, "Live-Spiele: Punkte für Spiel {MatchId} konnten nicht geladen werden.", match.Id);
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
                ApplyMatchState(match, state.Status, state.IsLive, state.HomeGoals, state.AwayGoals, state.Minute, now);
            }

            match.HomeTeamName = home.TeamName;
            match.AwayTeamName = away.TeamName;
            match.HomeTeamPoints = home.Points;
            match.AwayTeamPoints = away.Points;
            match.PointsUpdatedAt = now;
            ApplyPlayers(match, match.HomeTeamId, home.Players);
            ApplyPlayers(match, match.AwayTeamId, away.Players);
            refreshedCount++;
        }

        await dbContext.SaveChangesAsync(ct);

        if (refreshedCount > 0)
        {
            logger.LogInformation("Live-Spiele: Punkte für {Count} Spiel(e) aktualisiert.", refreshedCount);
        }

        return refreshedCount;
    }

    private void ApplyPlayers(LiveMatch match, string teamId, IReadOnlyList<KickbaseTeamCenterPlayerDto> players)
    {
        var existingRows = match.Players.Where(player => player.TeamId == teamId).ToDictionary(player => player.PlayerId);

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
            }

            row.PlayerName = player.Name;
            row.Points = player.Points;
            row.EventKeys = [.. player.EventKeys];
        }

        dbContext.LiveMatchPlayers.RemoveRange(existingRows.Values);
    }

    private static void ApplyMatchState(
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

        match.Status = status;
        match.HomeGoals = homeGoals;
        match.AwayGoals = awayGoals;
        match.Minute = minute;
    }

    private static bool NeedsPointsRefresh(LiveMatch match, DateTimeOffset now) =>
        match.PointsUpdatedAt is null
        || (match.Status != LiveMatchStatus.Finished && now - match.KickoffAt < MaxMatchDuration)
        || (match.FinishedAt is not null && now - match.FinishedAt < PostMatchRefreshWindow);

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
