namespace KickbaseAssistant.Infrastructure.Realtime;

public class LiveHubOptions
{
    public const string SectionName = "LiveHub";
    public const string PublishKeyHeader = "X-Live-Publish-Key";

    /// <summary>
    /// Same-server default: the Worker and Api run on the same machine (see Phase 3 deployment),
    /// so the Worker reaches the Api's SignalR hub over localhost, never through the public
    /// internet.
    /// </summary>
    public string Url { get; set; } = "http://127.0.0.1:5011/hubs/live";

    /// <summary>
    /// Shared secret between Worker and Api. The browser reaches the same hub through the public
    /// reverse proxy, so only connections presenting this key may call the hub's Publish* methods.
    /// Without a key the Api rejects every publish.
    /// </summary>
    public string PublishKey { get; set; } = string.Empty;
}
