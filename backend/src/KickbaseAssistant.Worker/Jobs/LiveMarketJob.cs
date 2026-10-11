using System.Text.Json;
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
                if (await CheckMarketAsync(league.Id, league.Name, ct))
                {
                    pushCount++;
                }

                if (await CheckActivityAsync(league.Id, league.Name, ct))
                {
                    pushCount++;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Live-Markt-Check: Liga {LeagueName} konnte nicht geprüft werden.", league.Name);
            }
        }

        if (pushCount > 0)
        {
            logger.LogInformation("Live-Markt-Check: {PushCount} Update(s) gepusht.", pushCount);
        }
    }

    private async Task<bool> CheckMarketAsync(string leagueId, string leagueName, CancellationToken ct)
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

    private async Task<bool> CheckActivityAsync(string leagueId, string leagueName, CancellationToken ct)
    {
        var activities = await kickbaseApiClient.GetActivitiesFeedAsync(leagueId, ct);
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

        var newTransfers = activities
            .Where(a => a.Type == TransferActivityType && a.OccurredAt > lastSeen)
            .OrderBy(a => a.OccurredAt)
            .Select(TryParseTransfer)
            .Where(t => t is not null)
            .ToList();

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
            transfers = newTransfers,
        };

        await liveHubClient.PublishMarketUpdateAsync(leagueId, payload, ct);
        return true;
    }

    /// <summary>
    /// "data" trägt je nach Transfer-Richtung entweder "byr" (Käufer, inneres "t"=1) oder "slr"
    /// (Verkäufer, inneres "t"=2) - siehe KickbaseActivityItemDto. Alles andere (z.B. zugeloste
    /// Spieler ohne byr/slr) wird übersprungen statt geraten.
    /// </summary>
    private static object? TryParseTransfer(KickbaseActivityItemDto activity)
    {
        var data = activity.Data;
        if (!data.TryGetProperty("pn", out var playerNameEl) || !data.TryGetProperty("trp", out var priceEl))
        {
            return null;
        }

        var hasBuyer = data.TryGetProperty("byr", out var buyerEl);
        var hasSeller = data.TryGetProperty("slr", out var sellerEl);
        if (!hasBuyer && !hasSeller)
        {
            return null;
        }

        return new
        {
            occurredAt = activity.OccurredAt,
            playerName = playerNameEl.GetString(),
            price = priceEl.GetInt64(),
            managerName = hasBuyer ? buyerEl.GetString() : sellerEl.GetString(),
            type = hasBuyer ? "Buy" : "Sell",
        };
    }

    private static bool IsQuietNightHour(DateTimeOffset utcNow)
    {
        var hour = TimeZoneInfo.ConvertTime(utcNow, GermanyTimeZone).Hour;
        return hour is >= 1 and < 7;
    }
}
