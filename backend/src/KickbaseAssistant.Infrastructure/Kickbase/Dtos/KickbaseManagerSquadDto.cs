using System.Text.Json.Serialization;

namespace KickbaseAssistant.Infrastructure.Kickbase.Dtos;

/// <summary>
/// Field names confirmed live against GET /v4/leagues/{leagueId}/managers/{managerId}/squad on
/// 2026-09-26. Same underlying player data as the own-squad endpoint, but keyed "pi"/"pn" here
/// instead of "i"/"n" - a different DTO rather than reusing KickbaseSquadPlayerDto.
/// </summary>
public class KickbaseManagerSquadPlayerDto
{
    [JsonPropertyName("pi")]
    public required string PlayerId { get; set; }

    [JsonPropertyName("pn")]
    public required string PlayerName { get; set; }

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

public class KickbaseManagerSquadResponseDto
{
    [JsonPropertyName("u")]
    public required string ManagerId { get; set; }

    [JsonPropertyName("unm")]
    public required string ManagerName { get; set; }

    [JsonPropertyName("it")]
    public required List<KickbaseManagerSquadPlayerDto> Players { get; set; }
}
