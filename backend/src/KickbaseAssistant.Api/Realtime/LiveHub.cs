using Microsoft.AspNetCore.SignalR;

namespace KickbaseAssistant.Api.Realtime;

/// <summary>
/// Live-Punkte push channel (Phase 6). The Worker connects as a privileged SignalR client (it's
/// the only thing with Kickbase credentials) and calls the Publish* methods whenever it polls a
/// live update; this hub then re-broadcasts it to every other connected client (the frontend).
///
/// No auth on the Publish* methods yet - acceptable for now because the Api is bound to 127.0.0.1
/// only (see Phase 3 deployment notes), so only same-machine processes can reach it at all. This MUST
/// gain real auth before the Api is ever exposed beyond localhost, same as the rest of the Api.
/// </summary>
public class LiveHub : Hub
{
    /// <summary>
    /// Umbauplan "Echtzeit-Aktualisierung": Transfermarkt-Änderungen (neue/entfernte Angebote,
    /// Preis-/Gebotsänderungen) und neue Liga-Transfers, von LiveMarketJob gepusht. Gleicher Hub,
    /// eigener Event-Name statt eines zweiten Hubs - die Verbindung/Lifecycle-Logik bleibt für
    /// Frontend und Worker identisch zu den anderen Publish-Methoden.
    /// </summary>
    public async Task PublishMarketUpdate(string leagueId, object payload)
    {
        await Clients.Others.SendAsync("MarketUpdated", leagueId, payload);
    }

    /// <summary>
    /// Eigener Kader oder Kontostand hat sich geändert (OwnSquadSyncJob) - reines Signal, das
    /// Frontend lädt daraufhin selbst neu (z.B. der Verkaufsplaner).
    /// </summary>
    public async Task PublishSquadUpdate(string leagueId)
    {
        await Clients.Others.SendAsync("SquadUpdated", leagueId);
    }

    public async Task PublishLiveMatchesUpdate(string competitionId)
    {
        await Clients.Others.SendAsync("LiveMatchesUpdated", competitionId);
    }
}
