using Microsoft.AspNetCore.SignalR;

namespace KickbaseAssistant.Api.Realtime;

/// <summary>
/// Live-Punkte push channel (Phase 6). The Worker connects as a privileged SignalR client (it's
/// the only thing with Kickbase credentials) and calls PublishRanking whenever it polls a live
/// update; this hub then re-broadcasts it to every other connected client (the future frontend).
///
/// No auth on PublishRanking yet - acceptable for now because the Api is bound to 127.0.0.1 only
/// (see Phase 3 deployment notes), so only same-machine processes can reach it at all. This MUST
/// gain real auth before the Api is ever exposed beyond localhost, same as the rest of the Api.
/// </summary>
public class LiveHub(ILogger<LiveHub> logger) : Hub
{
    public async Task PublishRanking(string leagueId, object payload)
    {
        logger.LogInformation("Live-Ranking-Update für Liga {LeagueId} empfangen, wird an Clients gebroadcastet.", leagueId);
        await Clients.Others.SendAsync("RankingUpdated", leagueId, payload);
    }
}
