using System.Text.Json.Serialization;

namespace KickbaseAssistant.Infrastructure.Kickbase.Dtos;

/// <summary>
/// Field names confirmed live against GET /v4/competitions/{competitionId}/players/{playerId}/
/// marketvalue/{timeframe} on 2026-09-21. Valid timeframe values per the community spec: 92 or
/// 365 (3 months / 1 year) - plain day counts like "30" 404 silently returned empty data instead
/// of erroring, which is what led to this being probed properly. "dt" is a day-index (days since
/// epoch, not a date/timestamp) - only used for ordering, never parsed as a calendar date.
/// </summary>
public class KickbaseMarketValueHistoryPointDto
{
    [JsonPropertyName("dt")]
    public long DayIndex { get; set; }

    [JsonPropertyName("mv")]
    public double MarketValue { get; set; }
}

public class KickbaseMarketValueHistoryResponseDto
{
    [JsonPropertyName("it")]
    public required List<KickbaseMarketValueHistoryPointDto> Points { get; set; }
}
