using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Odds;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quartz;

namespace KickbaseAssistant.Worker.Jobs;

[DisallowConcurrentExecution]
public class BettingOddsJob(
    OddsApiClient oddsApiClient,
    IOptions<OddsOptions> options,
    AppDbContext dbContext,
    ILogger<BettingOddsJob> logger) : IJob
{
    private static readonly TimeSpan KickoffTolerance = TimeSpan.FromHours(3);
    private const double MinimumNameSimilarity = 0.6;

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        if (!oddsApiClient.IsConfigured)
        {
            logger.LogDebug("Wettquoten: Odds:ApiKey ist nicht gesetzt - übersprungen.");
            return;
        }

        OddsResponse response;
        try
        {
            response = await oddsApiClient.GetOddsAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Wettquoten konnten nicht geladen werden.");
            return;
        }

        var competitionId = options.Value.CompetitionId;
        var now = DateTimeOffset.UtcNow;
        var candidateMatches = await dbContext.LiveMatches
            .Where(match => match.CompetitionId == competitionId && match.KickoffAt > now - KickoffTolerance)
            .ToListAsync(ct);
        var existingOdds = await dbContext.MatchOdds
            .Where(odds => odds.CompetitionId == competitionId)
            .ToDictionaryAsync(odds => odds.MatchId, ct);

        var unmatchedEvents = new List<string>();
        var matchedCount = 0;

        foreach (var oddsEvent in response.Events)
        {
            var match = FindMatch(oddsEvent, candidateMatches);
            if (match is null)
            {
                unmatchedEvents.Add($"{oddsEvent.HomeTeam} - {oddsEvent.AwayTeam}");
                continue;
            }

            var probabilities = oddsEvent.Bookmakers
                .Select(bookmaker => OddsCalculator.ToProbabilities(new OutcomeOdds(bookmaker.Home, bookmaker.Draw, bookmaker.Away)))
                .Where(probability => probability is not null)
                .Select(probability => probability!.Value)
                .ToList();
            if (probabilities.Count == 0)
            {
                continue;
            }

            var averageProbabilities = OddsCalculator.Average(probabilities);
            var expectedGoals = OddsCalculator.EstimateExpectedGoals(averageProbabilities);

            if (!existingOdds.TryGetValue(match.Id, out var odds))
            {
                odds = new MatchOdds
                {
                    MatchId = match.Id,
                    CompetitionId = competitionId,
                    SourceHomeTeam = oddsEvent.HomeTeam,
                    SourceAwayTeam = oddsEvent.AwayTeam,
                };
                dbContext.MatchOdds.Add(odds);
                existingOdds[match.Id] = odds;
            }

            odds.SourceHomeTeam = oddsEvent.HomeTeam;
            odds.SourceAwayTeam = oddsEvent.AwayTeam;
            odds.HomeOdds = oddsEvent.Bookmakers.Average(bookmaker => bookmaker.Home);
            odds.DrawOdds = oddsEvent.Bookmakers.Average(bookmaker => bookmaker.Draw);
            odds.AwayOdds = oddsEvent.Bookmakers.Average(bookmaker => bookmaker.Away);
            odds.HomeWinProbability = averageProbabilities.Home;
            odds.DrawProbability = averageProbabilities.Draw;
            odds.AwayWinProbability = averageProbabilities.Away;
            odds.ExpectedHomeGoals = expectedGoals.Home;
            odds.ExpectedAwayGoals = expectedGoals.Away;
            odds.BookmakerCount = oddsEvent.Bookmakers.Count;
            odds.UpdatedAt = now;
            matchedCount++;
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Wettquoten: {MatchedCount} von {EventCount} Spielen zugeordnet, noch {RemainingRequests} Abrufe im Kontingent.",
            matchedCount, response.Events.Count, response.RemainingRequests ?? "?");
        if (unmatchedEvents.Count > 0)
        {
            logger.LogWarning("Wettquoten: keinem Kickbase-Spiel zugeordnet: {Events}", string.Join("; ", unmatchedEvents));
        }
    }

    private static LiveMatch? FindMatch(OddsEvent oddsEvent, IReadOnlyList<LiveMatch> candidateMatches) =>
        candidateMatches
            .Where(match => (match.KickoffAt - oddsEvent.CommenceTime).Duration() <= KickoffTolerance)
            .Select(match => new
            {
                Match = match,
                HomeScore = BestSimilarity(oddsEvent.HomeTeam, match.HomeTeamName, match.HomeTeamSymbol),
                AwayScore = BestSimilarity(oddsEvent.AwayTeam, match.AwayTeamName, match.AwayTeamSymbol),
            })
            .Where(candidate => candidate.HomeScore >= MinimumNameSimilarity && candidate.AwayScore >= MinimumNameSimilarity)
            .OrderByDescending(candidate => candidate.HomeScore + candidate.AwayScore)
            .Select(candidate => candidate.Match)
            .FirstOrDefault();

    private static double BestSimilarity(string sourceName, params string?[] kickbaseNames) =>
        kickbaseNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => TeamNameMatcher.Similarity(sourceName, name!))
            .DefaultIfEmpty(0)
            .Max();
}
