using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Kickbase;
using KickbaseAssistant.Infrastructure.Kickbase.Dtos;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Infrastructure.Leagues;

public class LiveLeagueSyncService(KickbaseApiClient kickbaseApiClient, AppDbContext dbContext)
{
    private static readonly TimeSpan LineupSyncInterval = TimeSpan.FromMinutes(5);

    public async Task<bool> SyncAsync(League league, int matchdayNumber, bool matchesRefreshed, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var scores = await dbContext.LiveManagerScores
            .Include(score => score.Players)
            .Where(score => score.LeagueId == league.Id && score.MatchdayNumber == matchdayNumber)
            .ToListAsync(ct);

        var lineupsDue = scores.Count == 0
            || (matchesRefreshed && scores.Any(score => score.LineupSyncedAt is null || now - score.LineupSyncedAt >= LineupSyncInterval));
        if (!matchesRefreshed && !lineupsDue)
        {
            return false;
        }

        var ranking = await kickbaseApiClient.GetRankingAsync(league.Id, ct);
        var teamCenters = lineupsDue
            ? await LoadTeamCentersAsync(league, matchdayNumber, ct)
            : new Dictionary<string, KickbaseManagerTeamCenterDto>();

        var scoresByManager = scores.ToDictionary(score => score.ManagerId);
        if (teamCenters.TryGetValue(league.OwnUserId, out var ownTeamCenter))
        {
            var managerIds = ownTeamCenter.Users.Select(user => user.Id).ToHashSet();
            foreach (var departed in scores.Where(score => !managerIds.Contains(score.ManagerId)))
            {
                dbContext.LiveManagerScores.Remove(departed);
                scoresByManager.Remove(departed.ManagerId);
            }

            foreach (var user in ownTeamCenter.Users)
            {
                var score = GetOrAddScore(scoresByManager, league.Id, user, matchdayNumber);
                score.ManagerName = user.Name;
                score.Points = user.MatchdayPoints;
                score.PointsCounted = user.PointsCounted ?? true;
                score.MatchdayRank = user.MatchdayRank;
                ApplyLineup(score, teamCenters[user.Id]);
                score.LineupSyncedAt = now;
                score.UpdatedAt = now;
            }
        }

        var rankingIsForMatchday = ranking.CurrentMatchday == matchdayNumber;
        foreach (var rankingUser in ranking.Users)
        {
            if (!scoresByManager.TryGetValue(rankingUser.UserId, out var score))
            {
                continue;
            }

            score.ManagerName = rankingUser.Name;
            score.SeasonPoints = rankingUser.SeasonPoints;
            score.SeasonRank = rankingUser.SeasonRank;
            if (rankingIsForMatchday)
            {
                score.Points = rankingUser.MatchdayPoints;
                score.MatchdayRank = rankingUser.MatchdayRank;
                if (rankingUser.PointsCounted is { } pointsCounted)
                {
                    score.PointsCounted = pointsCounted;
                }
            }

            score.UpdatedAt = now;
        }

        return true;
    }

    private async Task<Dictionary<string, KickbaseManagerTeamCenterDto>> LoadTeamCentersAsync(
        League league, int matchdayNumber, CancellationToken ct)
    {
        var ownTeamCenter = await kickbaseApiClient.GetManagerTeamCenterAsync(league.Id, league.OwnUserId, matchdayNumber, ct);
        var teamCenters = new Dictionary<string, KickbaseManagerTeamCenterDto> { [league.OwnUserId] = ownTeamCenter };

        foreach (var user in ownTeamCenter.Users.Where(user => user.Id != league.OwnUserId))
        {
            teamCenters[user.Id] = await kickbaseApiClient.GetManagerTeamCenterAsync(league.Id, user.Id, matchdayNumber, ct);
        }

        return teamCenters;
    }

    private LiveManagerScore GetOrAddScore(
        Dictionary<string, LiveManagerScore> scoresByManager, string leagueId, KickbaseManagerTeamCenterUserDto user, int matchdayNumber)
    {
        if (scoresByManager.TryGetValue(user.Id, out var score))
        {
            return score;
        }

        score = new LiveManagerScore
        {
            LeagueId = leagueId,
            ManagerId = user.Id,
            ManagerName = user.Name,
            MatchdayNumber = matchdayNumber,
        };
        dbContext.LiveManagerScores.Add(score);
        scoresByManager[user.Id] = score;
        return score;
    }

    private void ApplyLineup(LiveManagerScore score, KickbaseManagerTeamCenterDto teamCenter)
    {
        var existingRows = score.Players.ToDictionary(player => player.PlayerId);
        var squad = teamCenter.Lineup.Select(player => (Player: player, InLineup: true))
            .Concat(teamCenter.Bench.Select(player => (Player: player, InLineup: false)))
            .DistinctBy(entry => entry.Player.Id);

        foreach (var (player, inLineup) in squad)
        {
            if (!existingRows.Remove(player.Id, out var row))
            {
                row = new LiveManagerPlayer
                {
                    PlayerId = player.Id,
                    PlayerName = player.Name,
                    TeamId = player.TeamId.ToString(),
                };
                score.Players.Add(row);
            }

            row.PlayerName = player.Name;
            row.TeamId = player.TeamId.ToString();
            row.MatchId = player.MatchId?.ToString();
            row.InLineup = inLineup;
        }

        dbContext.LiveManagerPlayers.RemoveRange(existingRows.Values);
    }
}
