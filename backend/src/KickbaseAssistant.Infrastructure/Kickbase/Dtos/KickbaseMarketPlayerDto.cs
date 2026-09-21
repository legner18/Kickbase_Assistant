using System.Text.Json.Serialization;

namespace KickbaseAssistant.Infrastructure.Kickbase.Dtos;

/// <summary>
/// Field names confirmed live against GET /v4/leagues/{leagueId}/market on 2026-09-21. Unlike
/// the squad endpoint (single "n"), market listings split first/last name ("fn"/"n") - "n" alone
/// still matches the short display-name convention used elsewhere (Player.Name), so only that is
/// mapped here for consistency.
/// </summary>
public class KickbaseMarketPlayerDto
{
    [JsonPropertyName("i")]
    public required string Id { get; set; }

    [JsonPropertyName("n")]
    public required string Name { get; set; }

    [JsonPropertyName("tid")]
    public required string TeamId { get; set; }

    [JsonPropertyName("pos")]
    public int Position { get; set; }

    [JsonPropertyName("mv")]
    public int MarketValue { get; set; }

    [JsonPropertyName("prc")]
    public int Price { get; set; }

    [JsonPropertyName("p")]
    public int TotalPoints { get; set; }

    [JsonPropertyName("ap")]
    public int AveragePoints { get; set; }

    [JsonPropertyName("ofc")]
    public int OfferCount { get; set; }

    /// <summary>Seconds until the listing expires.</summary>
    [JsonPropertyName("exs")]
    public int ExpiresInSeconds { get; set; }
}

public class KickbaseMarketResponseDto
{
    [JsonPropertyName("it")]
    public required List<KickbaseMarketPlayerDto> Players { get; set; }
}
