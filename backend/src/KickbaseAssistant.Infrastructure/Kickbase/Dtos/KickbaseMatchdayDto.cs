using System.Text.Json.Serialization;

namespace KickbaseAssistant.Infrastructure.Kickbase.Dtos;

/// <summary>
/// Field names confirmed live against GET /v4/competitions/{competitionId}/matchdays on
/// 2026-09-21. Response is a list of matchdays, each carrying its own list of matches.
/// Score/status fields confirmed live on 2026-10-27, outside any live match: "st" was 0 (not
/// started) or 2 (finished), "t1g"/"t2g"/"mtd" are absent before kickoff.
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

    [JsonPropertyName("t1sy")]
    public string? HomeTeamSymbol { get; set; }

    [JsonPropertyName("t2sy")]
    public string? AwayTeamSymbol { get; set; }

    [JsonPropertyName("t1g")]
    public int? HomeGoals { get; set; }

    [JsonPropertyName("t2g")]
    public int? AwayGoals { get; set; }

    [JsonPropertyName("st")]
    public int Status { get; set; }

    [JsonPropertyName("il")]
    public bool IsLive { get; set; }

    [JsonPropertyName("mtd")]
    public string? Minute { get; set; }
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
