using System.Collections.Concurrent;
using KickbaseAssistant.Domain.Players;

namespace KickbaseAssistant.Infrastructure.Realtime;

/// <summary>
/// In-memory "letzter bekannter Stand" für LiveMarketJob - singleton, damit er über die
/// Lebensdauer mehrerer Job-Ausführungen hinweg erhalten bleibt, ohne eine eigene DB-Tabelle für
/// rein transiente Zwischenzustände zu brauchen (vergleichbar mit KickbaseAuthService' Token-Cache).
/// Geht bei einem Worker-Neustart verloren - unkritisch, der erste Lauf nach einem Neustart seedet
/// den Stand einfach neu, ohne zu pushen (siehe LiveMarketJob).
/// </summary>
public class LiveMarketStateCache
{
    public ConcurrentDictionary<string, Dictionary<string, MarketListingSnapshot>> MarketByLeague { get; } = new();
    public ConcurrentDictionary<string, DateTimeOffset> LastActivitySeenByLeague { get; } = new();
}
