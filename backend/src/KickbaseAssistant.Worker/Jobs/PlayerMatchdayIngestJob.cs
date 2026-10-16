using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Kickbase;
using KickbaseAssistant.Infrastructure.Kickbase.Dtos;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace KickbaseAssistant.Worker.Jobs;

/// <summary>
/// Umbauplan "Datenbasis: tägliche Snapshots" (player_matchday) - Grundlage für Median,
/// S11-Quote und Pkt/Min (Phase 1 Kennzahlen), die sich aus den Saison-Aggregaten
/// (Player.TotalPoints/AveragePoints) nicht berechnen lassen.
///
/// Läuft für jeden Spieler, der in irgendeiner aktiven Liga im Kader steht (eigener Kader +
/// Mitspieler-Käder über SquadMembership; archivierte Ligen behalten ihre alten Zeilen, zählen
/// hier aber nicht mehr). Beim ersten Sichten eines Spielers werden alle von Kickbase
/// gelieferten Saisons rückwirkend geladen (einmaliger Backfill, gleiche Idee wie der
/// 92-Tage-Marktwert-Backfill in LeagueSquadSyncJob); danach wird nur noch die laufende Saison
/// aktualisiert, da vergangene Saisons sich nicht mehr ändern.
/// </summary>
public class PlayerMatchdayIngestJob(
    KickbaseApiClient kickbaseApiClient,
    AppDbContext dbContext,
    ILogger<PlayerMatchdayIngestJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var playerIds = await dbContext.SquadMemberships
            .Where(m => dbContext.Leagues.Any(l => l.Id == m.LeagueId && l.IsActive))
            .Select(m => m.PlayerId)
            .Distinct()
            .ToListAsync(ct);

        var playersIngested = 0;
        var matchdaysWritten = 0;

        foreach (var playerId in playerIds)
        {
            var player = await dbContext.Players.FindAsync([playerId], ct);
            if (player is null || player.CompetitionId <= 0)
            {
                continue;
            }

            var existingRows = await dbContext.PlayerMatchdayPerformances
                .Where(p => p.PlayerId == playerId)
                .ToDictionaryAsync(p => (p.SeasonId, p.MatchdayNumber), ct);
            var hasAnyPerformance = existingRows.Count > 0;

            IReadOnlyList<KickbasePlayerPerformanceSeasonDto> seasons;
            try
            {
                seasons = await kickbaseApiClient.GetPlayerPerformanceAsync(player.CompetitionId.ToString(), playerId, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Spieltags-Ingest: Performance für Spieler {PlayerId} ({PlayerName}) konnte nicht geladen werden.", playerId, player.Name);
                continue;
            }

            if (seasons.Count == 0)
            {
                continue;
            }

            var currentSeason = seasons.OrderByDescending(s => ParseSeasonId(s.SeasonId)).First();
            IReadOnlyList<KickbasePlayerPerformanceSeasonDto> seasonsToWrite = hasAnyPerformance ? [currentSeason] : seasons;

            foreach (var season in seasonsToWrite)
            {
                foreach (var matchday in season.Matchdays)
                {
                    var minutesPlayed = ParseMinutes(matchday.MinutesPlayedRaw);

                    if (!existingRows.TryGetValue((season.SeasonId, matchday.Day), out var existing))
                    {
                        dbContext.PlayerMatchdayPerformances.Add(new PlayerMatchdayPerformance
                        {
                            PlayerId = playerId,
                            SeasonId = season.SeasonId,
                            MatchdayNumber = matchday.Day,
                            Points = matchday.Points ?? 0,
                            MinutesPlayed = minutesPlayed,
                            StartedInLineup = minutesPlayed >= 60,
                        });
                        matchdaysWritten++;
                    }
                    else
                    {
                        existing.Points = matchday.Points ?? 0;
                        existing.MinutesPlayed = minutesPlayed;
                        existing.StartedInLineup = minutesPlayed >= 60;
                    }
                }
            }

            playersIngested++;
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Spieltags-Ingest fertig: {PlayerCount} Spieler verarbeitet, {MatchdayCount} neue Spieltags-Einträge.",
            playersIngested, matchdaysWritten);
    }

    private static int ParseMinutes(string? raw) =>
        raw is not null && int.TryParse(raw.TrimEnd('\''), out var minutes) ? minutes : 0;

    private static int ParseSeasonId(string seasonId) =>
        int.TryParse(seasonId, out var value) ? value : 0;
}
