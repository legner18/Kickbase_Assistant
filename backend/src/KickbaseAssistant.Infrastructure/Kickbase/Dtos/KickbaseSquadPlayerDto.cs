using System.Text.Json.Serialization;

namespace KickbaseAssistant.Infrastructure.Kickbase.Dtos;

/// <summary>
/// Field names confirmed live against GET /v4/leagues/{leagueId}/squad on 2026-09-21. Only the
/// fields Phase 2 (Marktwert-Tracker) needs are mapped; the raw response has more (status flags,
/// lineup order, trend deltas) that later phases (Aufstellungs-Wächter, Verkaufs-Alarm) can add.
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
}

public class KickbaseSquadResponseDto
{
    [JsonPropertyName("it")]
    public required List<KickbaseSquadPlayerDto> Players { get; set; }
}
