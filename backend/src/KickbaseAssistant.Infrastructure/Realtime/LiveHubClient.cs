using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KickbaseAssistant.Infrastructure.Realtime;

/// <summary>
/// The Worker's side of the Live-Punkte push channel: connects to the Api's SignalR hub as a
/// regular (privileged) client and calls PublishRanking, which the hub then re-broadcasts to
/// every other connected client (the future frontend). One long-lived connection, reused across
/// job runs and reconnected automatically if it drops - not one connect/call/disconnect per poll.
/// </summary>
public class LiveHubClient(IOptions<LiveHubOptions> options, ILogger<LiveHubClient> logger) : IAsyncDisposable
{
    private readonly HubConnection _connection = new HubConnectionBuilder()
        .WithUrl(options.Value.Url)
        .WithAutomaticReconnect()
        .Build();

    public async Task PublishRankingAsync(string leagueId, object payload, CancellationToken ct)
    {
        try
        {
            if (_connection.State == HubConnectionState.Disconnected)
            {
                await _connection.StartAsync(ct);
            }

            await _connection.InvokeAsync("PublishRanking", leagueId, payload, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SignalR-Publish an die Api fehlgeschlagen (LeagueId={LeagueId}).", leagueId);
        }
    }

    public async Task PublishMarketUpdateAsync(string leagueId, object payload, CancellationToken ct)
    {
        try
        {
            if (_connection.State == HubConnectionState.Disconnected)
            {
                await _connection.StartAsync(ct);
            }

            await _connection.InvokeAsync("PublishMarketUpdate", leagueId, payload, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SignalR-Markt-Publish an die Api fehlgeschlagen (LeagueId={LeagueId}).", leagueId);
        }
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
