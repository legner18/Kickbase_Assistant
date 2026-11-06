using System.Security.Cryptography;
using System.Text;
using KickbaseAssistant.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace KickbaseAssistant.Api.Realtime;

/// <summary>
/// Live-Punkte push channel (Phase 6). The Worker connects as a privileged SignalR client (it's
/// the only thing with Kickbase credentials) and calls the Publish* methods whenever it polls a
/// live update; this hub then re-broadcasts it to every other connected client (the frontend).
///
/// The browser reaches this hub through the public reverse proxy, so the Publish* methods only
/// accept connections that presented LiveHub:PublishKey in the X-Live-Publish-Key header.
/// </summary>
public class LiveHub(IOptions<LiveHubOptions> options, LiveWorkerStatusStore statusStore) : Hub
{
    private const string IsPublisherItem = "IsPublisher";

    public override async Task OnConnectedAsync()
    {
        var presentedKey = Context.GetHttpContext()?.Request.Headers[LiveHubOptions.PublishKeyHeader].ToString();
        var isPublisher = IsValidPublishKey(presentedKey);
        Context.Items[IsPublisherItem] = isPublisher;

        if (!isPublisher && statusStore.Current is { } status)
        {
            await Clients.Caller.SendAsync("LiveStatusUpdated", status);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Umbauplan "Echtzeit-Aktualisierung": Transfermarkt-Änderungen (neue/entfernte Angebote,
    /// Preis-/Gebotsänderungen) und neue Liga-Transfers, von LiveMarketJob gepusht. Gleicher Hub,
    /// eigener Event-Name statt eines zweiten Hubs - die Verbindung/Lifecycle-Logik bleibt für
    /// Frontend und Worker identisch zu den anderen Publish-Methoden.
    /// </summary>
    public async Task PublishMarketUpdate(string leagueId, object payload)
    {
        EnsurePublisher();
        await Clients.Others.SendAsync("MarketUpdated", leagueId, payload);
    }

    /// <summary>
    /// Eigener Kader oder Kontostand hat sich geändert (OwnSquadSyncJob) - reines Signal, das
    /// Frontend lädt daraufhin selbst neu (z.B. der Verkaufsplaner).
    /// </summary>
    public async Task PublishSquadUpdate(string leagueId)
    {
        EnsurePublisher();
        await Clients.Others.SendAsync("SquadUpdated", leagueId);
    }

    public async Task PublishLiveMatchesUpdate(string competitionId)
    {
        EnsurePublisher();
        await Clients.Others.SendAsync("LiveMatchesUpdated", competitionId);
    }

    public async Task PublishLiveStatus(LiveWorkerStatus status)
    {
        EnsurePublisher();
        statusStore.Current = status;
        await Clients.Others.SendAsync("LiveStatusUpdated", status);
    }

    private void EnsurePublisher()
    {
        if (Context.Items.TryGetValue(IsPublisherItem, out var isPublisher) && isPublisher is true)
        {
            return;
        }

        throw new HubException("Nur der Worker darf Live-Updates veröffentlichen.");
    }

    private bool IsValidPublishKey(string? presentedKey)
    {
        var expectedKey = options.Value.PublishKey;
        if (string.IsNullOrEmpty(expectedKey) || string.IsNullOrEmpty(presentedKey))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presentedKey), Encoding.UTF8.GetBytes(expectedKey));
    }
}
