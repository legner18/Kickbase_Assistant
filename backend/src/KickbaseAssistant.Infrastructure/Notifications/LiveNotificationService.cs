using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KickbaseAssistant.Infrastructure.Notifications;

public class LiveNotificationService(
    NtfyPushService ntfyPushService,
    LiveMatchStateCache stateCache,
    AppDbContext dbContext,
    ILogger<LiveNotificationService> logger)
{
    private static readonly Dictionary<int, (string Label, string Tag)> NotifiedEvents = new()
    {
        [1] = ("Tor", "soccer"),
        [2] = ("Eigentor", "see_no_evil"),
        [3] = ("Vorlage", "handshake"),
        [4] = ("Gelbe Karte", "yellow_square"),
        [8] = ("Eingewechselt", "arrow_up"),
    };

    public async Task NotifyPlayerEventsAsync(
        IReadOnlyList<LivePlayerEventChange> changes, IReadOnlyList<League> leagues, CancellationToken ct)
    {
        var notableChanges = changes
            .Select(change => (Change: change, EventKeys: change.NewEventKeys.Where(NotifiedEvents.ContainsKey).ToList()))
            .Where(entry => entry.EventKeys.Count > 0)
            .ToList();
        if (notableChanges.Count == 0 || leagues.Count == 0)
        {
            return;
        }

        try
        {
            var matchdayNumbers = notableChanges.Select(entry => entry.Change.Match.MatchdayNumber).Distinct().ToList();
            var ownPlayers = await LoadOwnPlayersAsync(matchdayNumbers, leagues, ct);

            foreach (var (change, eventKeys) in notableChanges)
            {
                if (ownPlayers.TryGetValue((change.Match.MatchdayNumber, change.PlayerId), out var inLineup))
                {
                    await SendPlayerEventAsync(change, eventKeys, inLineup, ct);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Live-Push: Ereignisse eigener Spieler konnten nicht gemeldet werden.");
        }
    }

    public async Task NotifyLeadChangeAsync(League league, int matchdayNumber, CancellationToken ct)
    {
        try
        {
            var standings = await dbContext.LiveManagerScores
                .AsNoTracking()
                .Where(score => score.LeagueId == league.Id && score.MatchdayNumber == matchdayNumber)
                .OrderByDescending(score => score.Points)
                .Select(score => new { score.ManagerId, score.ManagerName, score.Points })
                .ToListAsync(ct);
            if (standings.Count < 2)
            {
                return;
            }

            var leader = standings[0];
            var runnerUp = standings[1];
            var leaderId = leader.Points > 0 && leader.Points > runnerUp.Points ? leader.ManagerId : null;
            var stateKey = $"{league.Id}:{matchdayNumber}";

            if (!stateCache.MatchdayLeaderByLeague.TryGetValue(stateKey, out var previousLeaderId))
            {
                stateCache.MatchdayLeaderByLeague[stateKey] = leaderId;
                return;
            }

            if (leaderId is null || leaderId == previousLeaderId)
            {
                return;
            }

            stateCache.MatchdayLeaderByLeague[stateKey] = leaderId;

            if (leaderId == league.OwnUserId)
            {
                await ntfyPushService.SendAsync(
                    title: $"Du führst Spieltag {matchdayNumber} an",
                    message: $"{league.Name}: {leader.Points} Punkte, {leader.Points - runnerUp.Points} vor {runnerUp.ManagerName}.",
                    tags: "trophy",
                    ct: ct);
            }
            else if (previousLeaderId == league.OwnUserId)
            {
                var ownPoints = standings.FirstOrDefault(score => score.ManagerId == league.OwnUserId)?.Points;
                await ntfyPushService.SendAsync(
                    title: $"{leader.ManagerName} hat dich überholt",
                    message: $"{league.Name}, Spieltag {matchdayNumber}: {leader.ManagerName} {leader.Points} Punkte, du {ownPoints} Punkte.",
                    tags: "chart_with_downwards_trend",
                    ct: ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Live-Push: Führungswechsel in Liga {LeagueName} konnte nicht gemeldet werden.", league.Name);
        }
    }

    private async Task<Dictionary<(int MatchdayNumber, string PlayerId), bool>> LoadOwnPlayersAsync(
        IReadOnlyList<int> matchdayNumbers, IReadOnlyList<League> leagues, CancellationToken ct)
    {
        var leagueIds = leagues.Select(league => league.Id).ToList();
        var ownUserIds = leagues.Select(league => league.OwnUserId).ToList();

        var ownPlayers = await dbContext.LiveManagerPlayers
            .AsNoTracking()
            .Where(player => matchdayNumbers.Contains(player.Score!.MatchdayNumber)
                && leagueIds.Contains(player.Score.LeagueId)
                && ownUserIds.Contains(player.Score.ManagerId))
            .Select(player => new { player.Score!.MatchdayNumber, player.PlayerId, player.InLineup })
            .ToListAsync(ct);

        return ownPlayers
            .GroupBy(player => (player.MatchdayNumber, player.PlayerId))
            .ToDictionary(group => group.Key, group => group.Any(player => player.InLineup));
    }

    private Task SendPlayerEventAsync(LivePlayerEventChange change, IReadOnlyList<int> eventKeys, bool inLineup, CancellationToken ct)
    {
        var labels = eventKeys
            .GroupBy(eventKey => eventKey)
            .OrderBy(group => group.Key)
            .Select(group => (group.Count() > 1 ? $"{group.Count()}× " : string.Empty) + NotifiedEvents[group.Key].Label);

        var match = change.Match;
        var minute = string.IsNullOrEmpty(match.Minute) ? string.Empty : $" ({match.Minute}′)";
        var scoreLine = $"{match.HomeTeamName ?? match.HomeTeamSymbol} {match.HomeGoals ?? 0}:{match.AwayGoals ?? 0} "
            + $"{match.AwayTeamName ?? match.AwayTeamSymbol}{minute}";
        var lineupNote = inLineup ? string.Empty : " - nicht aufgestellt, zählt nicht";

        return ntfyPushService.SendAsync(
            title: $"{string.Join(" + ", labels)}: {change.PlayerName}",
            message: $"{scoreLine}. Jetzt {change.Points} Punkte{lineupNote}.",
            tags: NotifiedEvents[eventKeys.Min()].Tag,
            ct: ct);
    }
}
