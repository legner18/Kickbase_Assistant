using System.Text.Json.Serialization;

namespace KickbaseAssistant.Infrastructure.Kickbase.Dtos;

/// <summary>
/// Field names confirmed live against GET /v4/competitions/{competitionId}/matchdays on
/// 2026-09-21. Response is a list of matchdays, each carrying its own list of matches.
/// </summary>
public class KickbaseMatchDto
{
    [JsonPropertyName("mi")]
    public required string MatchId { get; set; }

    [JsonPropertyName("day")]
    public int Day { get; set; }

    [JsonPropertyName("dt")]
    public DateTimeOffset KickoffAt { get; set; }

    [JsonPropertyName("t1")]
    public required string HomeTeamId { get; set; }

    [JsonPropertyName("t2")]
    public required string AwayTeamId { get; set; }
}

public class KickbaseMatchdayDto
{
    [JsonPropertyName("day")]
    public int Day { get; set; }

    [JsonPropertyName("it")]
    public required List<KickbaseMatchDto> Matches { get; set; }
}

public class KickbaseMatchdaysResponseDto
{
    [JsonPropertyName("it")]
    public required List<KickbaseMatchdayDto> Matchdays { get; set; }
}
