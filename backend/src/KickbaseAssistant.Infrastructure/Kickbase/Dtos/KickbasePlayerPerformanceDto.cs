using System.Text.Json.Serialization;

namespace KickbaseAssistant.Infrastructure.Kickbase.Dtos;

/// <summary>
/// Field names confirmed live against GET /v4/competitions/{competitionId}/players/{playerId}/
/// performance on 2026-10-11. Response groups the player's matchday-by-matchday history by
/// season - the live sample returned 3 seasons (2024/2025, 2025/2026, 2026/2027), each with 34
/// "ph" (per-matchday) entries. "p" (points) is absent on matchdays the player did not play at
/// all. "mp" (minutes, a string like "72'") was expected on every entry from the single sample
/// player first checked, but a broader live run (2026-10-11) showed it's also sometimes entirely
/// absent - both are nullable, missing means "did not play". There is no explicit starting-XI
/// flag - only minutes played - PlayerMatchdayIngestJob approximates "started" as minutes >= 60.
/// </summary>
public class KickbasePlayerPerformanceMatchdayDto
{
    [JsonPropertyName("day")]
    public int Day { get; set; }

    [JsonPropertyName("p")]
    public int? Points { get; set; }

    [JsonPropertyName("mp")]
    public string? MinutesPlayedRaw { get; set; }
}

public class KickbasePlayerPerformanceSeasonDto
{
    [JsonPropertyName("sid")]
    public required string SeasonId { get; set; }

    [JsonPropertyName("ti")]
    public required string SeasonLabel { get; set; }

    [JsonPropertyName("ph")]
    public required List<KickbasePlayerPerformanceMatchdayDto> Matchdays { get; set; }
}

public class KickbasePlayerPerformanceResponseDto
{
    [JsonPropertyName("it")]
    public required List<KickbasePlayerPerformanceSeasonDto> Seasons { get; set; }
}
