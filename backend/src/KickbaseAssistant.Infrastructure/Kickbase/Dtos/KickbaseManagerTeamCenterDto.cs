using System.Text.Json.Serialization;

namespace KickbaseAssistant.Infrastructure.Kickbase.Dtos;

/// <summary>
/// Field names confirmed live against GET /v4/leagues/{leagueId}/users/{userId}/teamcenter?dayNumber={day}
/// on 2026-10-29 (finished matchdays, also for other managers of the league). The parameter must be
/// "dayNumber" - "day" is silently ignored and returns the latest matchday. "lp" is the manager's lineup
/// of that matchday, "nlp" the rest of the squad, "us" lists every manager with that matchday's points.
/// "tid"/"mi" are numbers here (strings in other endpoints). "pa" was false together with "mdp" 0 for a
/// manager whose lineup did score - Kickbase did not count that matchday for them.
/// </summary>
public class KickbaseManagerTeamCenterUserDto
{
    [JsonPropertyName("i")]
    public required string Id { get; set; }

    [JsonPropertyName("unm")]
    public required string Name { get; set; }

    [JsonPropertyName("mdp")]
    public int MatchdayPoints { get; set; }

    [JsonPropertyName("pa")]
    public bool? PointsCounted { get; set; }

    [JsonPropertyName("pl")]
    public int MatchdayRank { get; set; }
}

public class KickbaseManagerTeamCenterPlayerDto
{
    [JsonPropertyName("i")]
    public required string Id { get; set; }

    [JsonPropertyName("n")]
    public required string Name { get; set; }

    [JsonPropertyName("tid")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int TeamId { get; set; }

    [JsonPropertyName("mi")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? MatchId { get; set; }
}

public class KickbaseManagerTeamCenterDto
{
    [JsonPropertyName("us")]
    public List<KickbaseManagerTeamCenterUserDto> Users { get; set; } = [];

    [JsonPropertyName("lp")]
    public List<KickbaseManagerTeamCenterPlayerDto> Lineup { get; set; } = [];

    [JsonPropertyName("nlp")]
    public List<KickbaseManagerTeamCenterPlayerDto> Bench { get; set; } = [];
}
