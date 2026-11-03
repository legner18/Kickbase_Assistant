using System.Collections.Concurrent;

namespace KickbaseAssistant.Infrastructure.Realtime;

public class LiveMatchStateCache
{
    public ConcurrentDictionary<string, DateTimeOffset> ScheduleSyncedAtByCompetition { get; } = new();
    public IReadOnlyDictionary<int, string> EventTypeTitles { get; set; } = new Dictionary<int, string>();
    public DateTimeOffset? EventTypesLoadedAt { get; set; }
    public DateTimeOffset? PausedUntil { get; set; }
}
