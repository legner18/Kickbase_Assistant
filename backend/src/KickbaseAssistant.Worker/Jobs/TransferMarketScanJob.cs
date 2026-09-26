using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Kickbase;
using KickbaseAssistant.Infrastructure.Kickbase.Dtos;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace KickbaseAssistant.Worker.Jobs;

/// <summary>
/// Phase 7 (Transfermarkt-Scanner): scores every player currently listed on each active league's
/// transfer market from three equally-weighted 0-100 signals - exactly the three from the plan:
///
/// - Momentum: % market-value change over the last ~14 days of the 92-day history, linearly
///   mapped so +-10% hits the 0/100 ends (50 = flat). Deliberately simple/transparent, not ML.
/// - Lineup: latest LigaInsider-derived LineupPrediction status (Starting=100 ... Out=0). Already
///   covers all 18 Bundesliga teams as a side effect of LigaInsiderScrapeJob (see its notes).
/// - Fixtures: average league placement of the player's next up-to-3 opponents (placement 1 =
///   toughest, 18 = easiest), normalized to 0-100. A simplified stand-in for real difficulty
///   (doesn't account for home/away, form, etc.) - "Restprogramm" from the plan, kept basic.
///
/// This is a full-refresh snapshot job, not a history log: MarketScanResult rows for a league are
/// cleared and rewritten every run, since "what's on the market right now" is what matters.
/// </summary>
public class TransferMarketScanJob(
    KickbaseApiClient kickbaseApiClient,
    AppDbContext dbContext,
    ILogger<TransferMarketScanJob> logger) : IJob
{
    private const int MarketValueTimeframeDays = 92;
    private const int MomentumWindowDays = 14;
    private const int FixtureLookaheadMatches = 3;

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var activeLeagues = await dbContext.Leagues.Where(l => l.IsActive).ToListAsync(ct);

        var placementsByCompetition = new Dictionary<string, Dictionary<string, int>>();
        var teamNamesByCompetition = new Dictionary<string, Dictionary<string, string>>();
        var matchdaysByCompetition = new Dictionary<string, IReadOnlyList<KickbaseMatchdayDto>>();
        var totalScanned = 0;

        foreach (var league in activeLeagues)
        {
            var oldResults = await dbContext.MarketScanResults.Where(r => r.LeagueId == league.Id).ToListAsync(ct);
            dbContext.MarketScanResults.RemoveRange(oldResults);

            IReadOnlyList<KickbaseMarketPlayerDto> marketPlayers;
            try
            {
                marketPlayers = await kickbaseApiClient.GetMarketAsync(league.Id, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Transfermarkt-Scan: Markt für Liga {LeagueName} konnte nicht geladen werden.", league.Name);
                continue;
            }

            if (marketPlayers.Count == 0)
            {
                continue;
            }

            if (!placementsByCompetition.TryGetValue(league.CompetitionId, out var placements)
                || !teamNamesByCompetition.TryGetValue(league.CompetitionId, out var teamNames))
            {
                var table = await kickbaseApiClient.GetCompetitionTableAsync(league.CompetitionId, ct);
                placements = table.ToDictionary(t => t.Id, t => t.CurrentPlacement);
                teamNames = table.ToDictionary(t => t.Id, t => t.Name);
                placementsByCompetition[league.CompetitionId] = placements;
                teamNamesByCompetition[league.CompetitionId] = teamNames;
            }

            if (!matchdaysByCompetition.TryGetValue(league.CompetitionId, out var matchdays))
            {
                matchdays = await kickbaseApiClient.GetMatchdaysAsync(league.CompetitionId, ct);
                matchdaysByCompetition[league.CompetitionId] = matchdays;
            }

            var currentMatchday = (await kickbaseApiClient.GetRankingAsync(league.Id, ct)).CurrentMatchday;

            foreach (var marketPlayer in marketPlayers)
            {
                await UpsertPlayerAsync(marketPlayer, league.CompetitionId, teamNames, ct);

                var momentum = await ComputeMomentumAsync(league.CompetitionId, marketPlayer.Id, ct);
                var lineupScore = await ComputeLineupScoreAsync(marketPlayer.Id, ct);
                var fixtureScore = ComputeFixtureScore(marketPlayer.TeamId, matchdays, currentMatchday, placements);

                dbContext.MarketScanResults.Add(new MarketScanResult
                {
                    LeagueId = league.Id,
                    PlayerId = marketPlayer.Id,
                    Price = marketPlayer.Price,
                    OfferCount = marketPlayer.OfferCount,
                    ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(marketPlayer.ExpiresInSeconds),
                    MomentumScore = momentum.Score,
                    LineupScore = lineupScore,
                    FixtureScore = fixtureScore,
                    OverallScore = (momentum.Score + lineupScore + fixtureScore) / 3.0,
                    MarketValueChangeAbs = momentum.AbsChange,
                    MarketValueChangePercent = momentum.PercentChange,
                    ScannedAt = DateTimeOffset.UtcNow,
                });
                totalScanned++;
            }
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Transfermarkt-Scan fertig: {LeagueCount} Ligen geprüft, {PlayerCount} Marktspieler bewertet.",
            activeLeagues.Count, totalScanned);
    }

    private async Task UpsertPlayerAsync(
        KickbaseMarketPlayerDto marketPlayer, string competitionId, Dictionary<string, string> teamNames, CancellationToken ct)
    {
        var player = await dbContext.Players.FindAsync([marketPlayer.Id], ct);
        if (player is null)
        {
            player = new Player
            {
                Id = marketPlayer.Id,
                Name = marketPlayer.Name,
                TeamId = marketPlayer.TeamId,
                Position = marketPlayer.Position,
                CompetitionId = int.TryParse(competitionId, out var parsed) ? parsed : 0,
            };
            dbContext.Players.Add(player);
        }

        player.Name = marketPlayer.Name;
        player.TeamId = marketPlayer.TeamId;
        player.TeamName = teamNames.GetValueOrDefault(marketPlayer.TeamId, player.TeamName);
        player.Position = marketPlayer.Position;
        player.MarketValue = marketPlayer.MarketValue;
        player.TotalPoints = marketPlayer.TotalPoints;
        player.LastSeenAt = DateTimeOffset.UtcNow;
        // IsInSquad bleibt unangetastet - ein Marktangebot ist keine Kader-Mitgliedschaft.
    }

    private async Task<MomentumResult> ComputeMomentumAsync(string competitionId, string playerId, CancellationToken ct)
    {
        try
        {
            var history = await kickbaseApiClient.GetMarketValueHistoryAsync(
                competitionId, playerId, MarketValueTimeframeDays, ct);
            if (history.Count < 2)
            {
                return new MomentumResult(50, null, null);
            }

            var window = history.Count > MomentumWindowDays
                ? history.Skip(history.Count - MomentumWindowDays).ToList()
                : history;
            var first = window[0].MarketValue;
            var last = window[^1].MarketValue;
            if (first <= 0)
            {
                return new MomentumResult(50, null, null);
            }

            var pctChange = (last - first) / first;
            var score = Math.Clamp(50 + pctChange * 500, 0, 100);
            return new MomentumResult(score, last - first, pctChange * 100);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Marktwert-Historie für Spieler {PlayerId} konnte nicht geladen werden.", playerId);
            return new MomentumResult(50, null, null);
        }
    }

    private sealed record MomentumResult(double Score, double? AbsChange, double? PercentChange);

    private async Task<double> ComputeLineupScoreAsync(string playerId, CancellationToken ct)
    {
        var latest = await dbContext.LineupPredictions
            .Where(l => l.PlayerId == playerId)
            .OrderByDescending(l => l.ScrapedAt)
            .FirstOrDefaultAsync(ct);

        return latest?.Status switch
        {
            LineupStatus.Starting => 100,
            LineupStatus.Doubtful => 50,
            LineupStatus.Bench => 20,
            LineupStatus.Out => 0,
            _ => 50,
        };
    }

    private static double ComputeFixtureScore(
        string teamId,
        IReadOnlyList<KickbaseMatchdayDto> matchdays,
        int currentMatchday,
        Dictionary<string, int> placements)
    {
        var upcomingOpponents = matchdays
            .Where(md => md.Day > currentMatchday)
            .OrderBy(md => md.Day)
            .SelectMany(md => md.Matches)
            .Where(m => m.HomeTeamId == teamId || m.AwayTeamId == teamId)
            .Take(FixtureLookaheadMatches)
            .Select(m => m.HomeTeamId == teamId ? m.AwayTeamId : m.HomeTeamId)
            .ToList();

        var opponentPlacements = upcomingOpponents
            .Where(placements.ContainsKey)
            .Select(id => placements[id])
            .ToList();

        if (opponentPlacements.Count == 0 || placements.Count == 0)
        {
            return 50;
        }

        return Math.Clamp(opponentPlacements.Average() / placements.Count * 100, 0, 100);
    }
}
