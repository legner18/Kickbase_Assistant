using System.Text.Json.Serialization;

namespace KickbaseAssistant.Infrastructure.Kickbase.Dtos;

/// <summary>
/// Field names confirmed live against GET /v4/leagues on 2026-09-21.
/// </summary>
public class KickbaseLeagueDto
{
    [JsonPropertyName("i")]
    public required string Id { get; set; }

    [JsonPropertyName("n")]
    public required string Name { get; set; }

    [JsonPropertyName("cpi")]
    public required string CompetitionId { get; set; }
}

public class KickbaseLeaguesResponseDto
{
    [JsonPropertyName("lins")]
    public required List<KickbaseLeagueDto> Leagues { get; set; }
}
