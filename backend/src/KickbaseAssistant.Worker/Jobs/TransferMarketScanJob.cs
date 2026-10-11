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
///   toughest, 18 = easiest), normalized to 0-100 via FixtureDifficultyCalculator.ComputeScore -
///   "Restprogramm" from the plan, kept basic (doesn't account for home/away, form, etc.).
///
/// This is a full-refresh snapshot job, not a history log: MarketScanResult rows for a league are
/// cleared and rewritten every run, since "what's on the market right now" is what matters.
///
/// The same three signals are also applied to the user's own squad in each league (SquadPlayerScore,
/// same full-refresh semantics) - a "sell recommendation" view rather than a "buy recommendation"
/// one: a LOW score on an owned player means bad momentum/lineup/fixtures, worth reviewing for a
/// sale. Reuses the per-league table/matchday/ranking data already fetched for market scoring, no
/// extra Kickbase calls.
///
/// Also owns TeamFixture (Umbauplan "Gegnerstärke"): once per competition per run, the next 5
/// fixtures for every team in that competition (not just teams with known players) are written via
/// FixtureDifficultyCalculator.ComputeUpcomingFixtures, so the player-detail page has data for any
/// player regardless of whether they're currently on a market/squad.
/// </summary>
public class TransferMarketScanJob(
    KickbaseApiClient kickbaseApiClient,
    AppDbContext dbContext,
    ILogger<TransferMarketScanJob> logger) : IJob
{
    private const int MarketValueTimeframeDays = 92;
    private const int MomentumWindowDays = 14;

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var activeLeagues = await dbContext.Leagues.Where(l => l.IsActive).ToListAsync(ct);

        var placementsByCompetition = new Dictionary<string, Dictionary<string, int>>();
        var teamNamesByCompetition = new Dictionary<string, Dictionary<string, string>>();
        var matchdaysByCompetition = new Dictionary<string, IReadOnlyList<KickbaseMatchdayDto>>();
        var flatMatchesByCompetition = new Dictionary<string, List<FixtureMatch>>();
        var teamFixturesWritten = new HashSet<string>();
        var totalScanned = 0;

        var totalSquadScored = 0;

        foreach (var league in activeLeagues)
        {
            var oldResults = await dbContext.MarketScanResults.Where(r => r.LeagueId == league.Id).ToListAsync(ct);
            dbContext.MarketScanResults.RemoveRange(oldResults);
            var oldSquadScores = await dbContext.SquadPlayerScores.Where(r => r.LeagueId == league.Id).ToListAsync(ct);
            dbContext.SquadPlayerScores.RemoveRange(oldSquadScores);

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

            Dictionary<string, int> placements;
            Dictionary<string, string> teamNames;
            IReadOnlyList<KickbaseMatchdayDto> matchdays;
            int currentMatchday;
            try
            {
                if (!placementsByCompetition.TryGetValue(league.CompetitionId, out placements!)
                    || !teamNamesByCompetition.TryGetValue(league.CompetitionId, out teamNames!))
                {
                    var table = await kickbaseApiClient.GetCompetitionTableAsync(league.CompetitionId, ct);
                    placements = table.ToDictionary(t => t.Id, t => t.CurrentPlacement);
                    teamNames = table.ToDictionary(t => t.Id, t => t.Name);
                    placementsByCompetition[league.CompetitionId] = placements;
                    teamNamesByCompetition[league.CompetitionId] = teamNames;
                }

                if (!matchdaysByCompetition.TryGetValue(league.CompetitionId, out matchdays!))
                {
                    matchdays = await kickbaseApiClient.GetMatchdaysAsync(league.CompetitionId, ct);
                    matchdaysByCompetition[league.CompetitionId] = matchdays;
                    flatMatchesByCompetition[league.CompetitionId] = matchdays
                        .SelectMany(md => md.Matches.Select(m => new FixtureMatch(md.Day, m.HomeTeamId, m.AwayTeamId)))
                        .ToList();
                }

                currentMatchday = (await kickbaseApiClient.GetRankingAsync(league.Id, ct)).CurrentMatchday;

                if (teamFixturesWritten.Add(league.CompetitionId))
                {
                    await WriteTeamFixturesAsync(league.CompetitionId, flatMatchesByCompetition[league.CompetitionId], currentMatchday, placements, teamNames, ct);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Transfermarkt-Scan: Tabellen-/Spieltag-Daten für Liga {LeagueName} konnten nicht geladen werden.", league.Name);
                continue;
            }

            foreach (var marketPlayer in marketPlayers)
            {
                await UpsertPlayerAsync(marketPlayer, league.CompetitionId, teamNames, ct);

                var momentum = await ComputeMomentumAsync(league.CompetitionId, marketPlayer.Id, ct);
                var lineupScore = await ComputeLineupScoreAsync(marketPlayer.Id, ct);
                var fixtureScore = FixtureDifficultyCalculator.ComputeScore(
                    marketPlayer.TeamId, flatMatchesByCompetition[league.CompetitionId], currentMatchday, placements);

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

            var ownSquad = await dbContext.SquadMemberships
                .Where(m => m.LeagueId == league.Id && m.ManagerId == league.OwnUserId)
                .Select(m => m.Player!)
                .ToListAsync(ct);

            foreach (var player in ownSquad)
            {
                var momentum = await ComputeMomentumAsync(league.CompetitionId, player.Id, ct);
                var lineupScore = await ComputeLineupScoreAsync(player.Id, ct);
                var fixtureScore = FixtureDifficultyCalculator.ComputeScore(
                    player.TeamId, flatMatchesByCompetition[league.CompetitionId], currentMatchday, placements);

                dbContext.SquadPlayerScores.Add(new SquadPlayerScore
                {
                    LeagueId = league.Id,
                    PlayerId = player.Id,
                    MomentumScore = momentum.Score,
                    LineupScore = lineupScore,
                    FixtureScore = fixtureScore,
                    OverallScore = (momentum.Score + lineupScore + fixtureScore) / 3.0,
                    MarketValueChangeAbs = momentum.AbsChange,
                    MarketValueChangePercent = momentum.PercentChange,
                    ScannedAt = DateTimeOffset.UtcNow,
                });
                totalSquadScored++;
            }
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Transfermarkt-Scan fertig: {LeagueCount} Ligen geprüft, {PlayerCount} Marktspieler bewertet, {SquadCount} eigene Kaderspieler bewertet.",
            activeLeagues.Count, totalScanned, totalSquadScored);
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
        player.AveragePoints = marketPlayer.AveragePoints;
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

    private async Task WriteTeamFixturesAsync(
        string competitionId,
        List<FixtureMatch> flatMatches,
        int currentMatchday,
        Dictionary<string, int> placements,
        Dictionary<string, string> teamNames,
        CancellationToken ct)
    {
        var oldFixtures = await dbContext.TeamFixtures.Where(f => f.CompetitionId == competitionId).ToListAsync(ct);
        dbContext.TeamFixtures.RemoveRange(oldFixtures);

        foreach (var teamId in placements.Keys)
        {
            var upcoming = FixtureDifficultyCalculator.ComputeUpcomingFixtures(teamId, flatMatches, currentMatchday, placements);
            foreach (var fixture in upcoming)
            {
                dbContext.TeamFixtures.Add(new TeamFixture
                {
                    CompetitionId = competitionId,
                    TeamId = teamId,
                    MatchdayNumber = fixture.Day,
                    OpponentTeamId = fixture.OpponentTeamId,
                    OpponentTeamName = teamNames.GetValueOrDefault(fixture.OpponentTeamId),
                    IsHome = fixture.IsHome,
                    DifficultyTier = fixture.DifficultyTier,
                });
            }
        }
    }
}
