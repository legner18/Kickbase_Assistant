using System.Text.Json;
using System.Text.Json.Serialization;

namespace KickbaseAssistant.Infrastructure.Kickbase.Dtos;

/// <summary>
/// Field names confirmed live against GET /v4/leagues/{leagueId}/activitiesFeed on 2026-10-11.
/// A single chronological feed mixing several activity kinds - only "t" (type) is mapped at the
/// envelope level, "data"'s shape depends on it. Confirmed: type 15 is a completed transfer
/// (buy/sell), identified by the presence of "byr" (buyer name) or "slr" (seller name) in "data" -
/// see LiveMarketJob.TryParseTransfer. Other observed type codes (e.g. 3, seen on generic
/// player/market-value items) are not decoded - not needed for the live transfer feed, and not
/// confirmed enough to trust a typed shape for.
/// </summary>
public class KickbaseActivityItemDto
{
    [JsonPropertyName("i")]
    public required string Id { get; set; }

    [JsonPropertyName("t")]
    public int Type { get; set; }

    [JsonPropertyName("dt")]
    public DateTimeOffset OccurredAt { get; set; }

    [JsonPropertyName("data")]
    public JsonElement Data { get; set; }
}

public class KickbaseActivityFeedResponseDto
{
    [JsonPropertyName("af")]
    public required List<KickbaseActivityItemDto> Activities { get; set; }
}
