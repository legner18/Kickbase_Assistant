using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Kickbase;
using KickbaseAssistant.Infrastructure.Kickbase.Dtos;
using KickbaseAssistant.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace KickbaseAssistant.Worker.Jobs;

/// <summary>
/// Umbauplan "Echtzeit-Aktualisierung": pollt Transfermarkt-Angebote und Liga-Transfer-Aktivität
/// alle 60s und pusht nur echte Änderungen (siehe MarketChangeDetector) per SignalR, statt dem
/// Frontend volle Listen zu schicken - "nur Deltas pushen" aus dem Plan. Bewusst von
/// TransferMarketScanJob getrennt: dessen Momentum/Aufstellung/Restprogramm-Scoring braucht pro
/// Spieler mehrere Kickbase-Aufrufe (u.a. 92-Tage-Marktwert-Historie) und bleibt bei 2h, damit der
/// Account nicht durch sekündliches Scoring auffällt - dieser Job macht pro Liga nur die zwei
/// ohnehin schon vorhandenen, leichten Aufrufe (Markt + Activity-Feed).
///
/// Erster Lauf pro Liga (nach Worker-Start) seedet den Cache nur, ohne zu pushen - sonst würde
/// jedes bestehende Markt-Angebot als "neu" gemeldet.
///
/// Schreibt außerdem MarketListingHistory (Umbauplan "Zuletzt auf dem Markt") - Activities werden
/// pro Lauf nur einmal geladen und sowohl für den Live-Transfer-Push als auch für die
/// Sold/Expired-Erkennung einer verschwundenen Listung wiederverwendet (kein Extra-Aufruf).
/// </summary>
public class LiveMarketJob(
    KickbaseApiClient kickbaseApiClient,
    LiveHubClient liveHubClient,
    LiveMarketStateCache stateCache,
    AppDbContext dbContext,
    ILogger<LiveMarketJob> logger) : IJob
{
    private const int TransferActivityType = 15;
    private static readonly TimeZoneInfo GermanyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        if (IsQuietNightHour(DateTimeOffset.UtcNow))
        {
            return;
        }

        var activeLeagues = await dbContext.Leagues.Where(l => l.IsActive).ToListAsync(ct);
        var pushCount = 0;

        foreach (var league in activeLeagues)
        {
            try
            {
                var activities = await kickbaseApiClient.GetActivitiesFeedAsync(league.Id, ct);
                var parsedTransfers = activities
                    .Where(a => a.Type == TransferActivityType)
                    .Select(TryParseTransfer)
                    .Where(t => t is not null)
                    .Select(t => t!)
                    .ToList();

                if (await CheckMarketAsync(league.Id, league.Name, parsedTransfers, ct))
                {
                    pushCount++;
                }

                if (await CheckActivityAsync(league.Id, league.Name, activities, parsedTransfers, ct))
                {
                    pushCount++;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Live-Markt-Check: Liga {LeagueName} konnte nicht geprüft werden.", league.Name);
            }
        }

        await dbContext.SaveChangesAsync(ct);

        if (pushCount > 0)
        {
            logger.LogInformation("Live-Markt-Check: {PushCount} Update(s) gepusht.", pushCount);
        }
    }

    private async Task<bool> CheckMarketAsync(
        string leagueId, string leagueName, List<ParsedTransferActivity> parsedTransfers, CancellationToken ct)
    {
        var listings = await kickbaseApiClient.GetMarketAsync(leagueId, ct);
        var current = listings.ToDictionary(
            p => p.Id,
            p => new MarketListingSnapshot(p.Id, p.Name, p.Price, p.OfferCount, DateTimeOffset.UtcNow.AddSeconds(p.ExpiresInSeconds)));

        if (!stateCache.MarketByLeague.TryGetValue(leagueId, out var previous))
        {
            stateCache.MarketByLeague[leagueId] = current;
            return false;
        }

        stateCache.MarketByLeague[leagueId] = current;
        var changes = MarketChangeDetector.Detect(previous, current);

        await RecordListingHistoryAsync(leagueId, changes, parsedTransfers, ct);

        if (changes.Count == 0)
        {
            return false;
        }

        var payload = new
        {
            leagueId,
            leagueName,
            kind = "listings",
            changes = changes.Select(c => new
            {
                type = c.Type.ToString(),
                playerId = c.PlayerId,
                playerName = c.PlayerName,
                previousValue = c.PreviousValue,
                currentValue = c.CurrentValue,
            }),
        };

        await liveHubClient.PublishMarketUpdateAsync(leagueId, payload, ct);
        return true;
    }

    /// <summary>
    /// New -> öffnet einen MarketListingHistory-Eintrag. Removed -> schließt den offenen Eintrag
    /// für diesen Spieler; Sold, wenn im selben Lauf eine passende Transfer-Activity für denselben
    /// Spieler gefunden wurde, sonst Expired (Best-Effort, siehe MarketListingHistory-Doku).
    /// </summary>
    private async Task RecordListingHistoryAsync(
        string leagueId, IReadOnlyList<MarketChange> changes, List<ParsedTransferActivity> parsedTransfers, CancellationToken ct)
    {
        foreach (var change in changes)
        {
            if (change.Type == MarketChangeType.New)
            {
                dbContext.MarketListingHistories.Add(new MarketListingHistory
                {
                    LeagueId = leagueId,
                    PlayerId = change.PlayerId,
                    PlayerName = change.PlayerName,
                    ListedAt = DateTimeOffset.UtcNow,
                    Price = change.CurrentValue ?? 0,
                    Outcome = MarketListingOutcome.Open,
                });
            }
            else if (change.Type == MarketChangeType.Removed)
            {
                var open = await dbContext.MarketListingHistories
                    .Where(h => h.LeagueId == leagueId && h.PlayerId == change.PlayerId && h.Outcome == MarketListingOutcome.Open)
                    .OrderByDescending(h => h.ListedAt)
                    .FirstOrDefaultAsync(ct);
                if (open is null)
                {
                    continue; // Listung existierte schon vor dem ersten Worker-Start nach diesem Feature.
                }

                open.RemovedAt = DateTimeOffset.UtcNow;
                open.Outcome = parsedTransfers.Any(t => t.PlayerId == change.PlayerId)
                    ? MarketListingOutcome.Sold
                    : MarketListingOutcome.Expired;
            }
        }
    }

    private async Task<bool> CheckActivityAsync(
        string leagueId, string leagueName, IReadOnlyList<KickbaseActivityItemDto> activities,
        List<ParsedTransferActivity> parsedTransfers, CancellationToken ct)
    {
        if (activities.Count == 0)
        {
            return false;
        }

        var latestSeen = activities.Max(a => a.OccurredAt);

        if (!stateCache.LastActivitySeenByLeague.TryGetValue(leagueId, out var lastSeen))
        {
            stateCache.LastActivitySeenByLeague[leagueId] = latestSeen;
            return false;
        }

        var newTransfers = parsedTransfers.Where(t => t.OccurredAt > lastSeen).OrderBy(t => t.OccurredAt).ToList();
        stateCache.LastActivitySeenByLeague[leagueId] = latestSeen;

        if (newTransfers.Count == 0)
        {
            return false;
        }

        var payload = new
        {
            leagueId,
            leagueName,
            kind = "transfers",
            transfers = newTransfers.Select(t => new
            {
                occurredAt = t.OccurredAt,
                playerName = t.PlayerName,
                price = t.Price,
                managerName = t.ManagerName,
                type = t.Type,
            }),
        };

        await liveHubClient.PublishMarketUpdateAsync(leagueId, payload, ct);
        return true;
    }

    /// <summary>
    /// "data" trägt je nach Transfer-Richtung entweder "byr" (Käufer, inneres "t"=1) oder "slr"
    /// (Verkäufer, inneres "t"=2) - siehe KickbaseActivityItemDto. Alles andere (z.B. zugeloste
    /// Spieler ohne byr/slr) wird übersprungen statt geraten.
    /// </summary>
    private static ParsedTransferActivity? TryParseTransfer(KickbaseActivityItemDto activity)
    {
        var data = activity.Data;
        if (!data.TryGetProperty("pi", out var playerIdEl)
            || !data.TryGetProperty("pn", out var playerNameEl)
            || !data.TryGetProperty("trp", out var priceEl))
        {
            return null;
        }

        var hasBuyer = data.TryGetProperty("byr", out var buyerEl);
        var hasSeller = data.TryGetProperty("slr", out var sellerEl);
        if (!hasBuyer && !hasSeller)
        {
            return null;
        }

        return new ParsedTransferActivity(
            playerIdEl.GetString() ?? "",
            playerNameEl.GetString() ?? "",
            activity.OccurredAt,
            priceEl.GetInt64(),
            (hasBuyer ? buyerEl.GetString() : sellerEl.GetString()) ?? "",
            hasBuyer ? "Buy" : "Sell");
    }

    private static bool IsQuietNightHour(DateTimeOffset utcNow)
    {
        var hour = TimeZoneInfo.ConvertTime(utcNow, GermanyTimeZone).Hour;
        return hour is >= 1 and < 7;
    }

    private sealed record ParsedTransferActivity(
        string PlayerId, string PlayerName, DateTimeOffset OccurredAt, long Price, string ManagerName, string Type);
}
