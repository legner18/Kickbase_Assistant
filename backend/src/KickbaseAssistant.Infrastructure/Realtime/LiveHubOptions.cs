namespace KickbaseAssistant.Infrastructure.Realtime;

public class LiveHubOptions
{
    public const string SectionName = "LiveHub";

    /// <summary>
    /// Same-server default: the Worker and Api run on the same machine (see Phase 3 deployment),
    /// so the Worker reaches the Api's SignalR hub over localhost, never through the public
    /// internet.
    /// </summary>
    public string Url { get; set; } = "http://127.0.0.1:5011/hubs/live";
}
