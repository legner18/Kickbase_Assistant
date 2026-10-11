namespace KickbaseAssistant.Domain.Players;

public readonly record struct MarketListingSnapshot(
    string PlayerId, string PlayerName, int Price, int OfferCount, DateTimeOffset ExpiresAt);

public enum MarketChangeType
{
    New,
    Removed,
    PriceChanged,
    OfferCountChanged,
}

public readonly record struct MarketChange(
    MarketChangeType Type, string PlayerId, string PlayerName, int? PreviousValue, int? CurrentValue);

/// <summary>
/// Umbauplan "Echtzeit-Aktualisierung": reine Diff-Logik zwischen zwei Transfermarkt-Momentaufnahmen,
/// damit LiveMarketJob nur bei echten Änderungen pusht statt bei jedem 60s-Poll unverändert erneut.
/// Bewusst ohne DB/HTTP-Abhängigkeiten gehalten, damit sie sich mit festen Beispiel-Listen testen
/// lässt - gleiches Prinzip wie BalanceGuardCalculator/RosterCheck.
/// </summary>
public static class MarketChangeDetector
{
    public static IReadOnlyList<MarketChange> Detect(
        IReadOnlyDictionary<string, MarketListingSnapshot> previous,
        IReadOnlyDictionary<string, MarketListingSnapshot> current)
    {
        var changes = new List<MarketChange>();

        foreach (var (playerId, listing) in current)
        {
            if (!previous.TryGetValue(playerId, out var prev))
            {
                changes.Add(new MarketChange(MarketChangeType.New, playerId, listing.PlayerName, null, listing.Price));
                continue;
            }

            if (prev.Price != listing.Price)
            {
                changes.Add(new MarketChange(MarketChangeType.PriceChanged, playerId, listing.PlayerName, prev.Price, listing.Price));
            }

            if (prev.OfferCount != listing.OfferCount)
            {
                changes.Add(new MarketChange(MarketChangeType.OfferCountChanged, playerId, listing.PlayerName, prev.OfferCount, listing.OfferCount));
            }
        }

        foreach (var (playerId, prev) in previous)
        {
            if (!current.ContainsKey(playerId))
            {
                changes.Add(new MarketChange(MarketChangeType.Removed, playerId, prev.PlayerName, prev.Price, null));
            }
        }

        return changes;
    }
}
