using System.Text.Json.Serialization;

namespace KickbaseAssistant.Infrastructure.Kickbase.Dtos;

/// <summary>
/// Field names confirmed live against GET /v4/leagues/{leagueId}/squad on 2026-09-21. Only the
/// fields Phase 2 (Marktwert-Tracker) needs are mapped; the raw response has more (status flags,
/// lineup order, trend deltas) that later phases (Aufstellungs-Wächter, Verkaufs-Alarm) can add.
///
/// "p"/"ap" (total/average points) added 2026-09-26 - same field names already confirmed live on
/// the market ("p"/"ap" in KickbaseMarketPlayerDto) and other-manager-squad ("p"/"ap" in
/// KickbaseManagerSquadPlayerDto) endpoints; this squad endpoint was the one place points were
/// never mapped, which silently left the user's own squad stuck at 0 points in the DB forever.
/// </summary>
public class KickbaseSquadPlayerDto
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

    [JsonPropertyName("p")]
    public int TotalPoints { get; set; }

    [JsonPropertyName("ap")]
    public int AveragePoints { get; set; }
}

public class KickbaseSquadResponseDto
{
    [JsonPropertyName("it")]
    public required List<KickbaseSquadPlayerDto> Players { get; set; }
}
