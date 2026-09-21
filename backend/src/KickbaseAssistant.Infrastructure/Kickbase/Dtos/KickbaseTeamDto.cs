using System.Text.Json.Serialization;

namespace KickbaseAssistant.Infrastructure.Kickbase.Dtos;

/// <summary>
/// Field names confirmed live against GET /v4/competitions/{id}/table on 2026-09-21. Only "tid"
/// and "tn" are mapped - the endpoint is a league table and also carries points/rank/goal-diff,
/// which Phase 8 (Liga-Analyse) can add if needed.
/// </summary>
public class KickbaseTeamDto
{
    [JsonPropertyName("tid")]
    public required string Id { get; set; }

    [JsonPropertyName("tn")]
    public required string Name { get; set; }

    /// <summary>Current league placement (1 = top of the table = toughest opponent).</summary>
    [JsonPropertyName("cpl")]
    public int CurrentPlacement { get; set; }
}

public class KickbaseTableResponseDto
{
    [JsonPropertyName("it")]
    public required List<KickbaseTeamDto> Teams { get; set; }
}
