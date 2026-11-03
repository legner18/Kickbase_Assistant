using System.Text.Json.Serialization;

namespace KickbaseAssistant.Infrastructure.Kickbase.Dtos;

/// <summary>
/// Field names confirmed live against GET /v4/competitions/{competitionId}/teams/{teamId}/
/// teamcenter?day={day} on 2026-10-27 (finished match, no live match was running). "it" only lists
/// players who played, the team's "p" is the sum of their "p". "k" holds one key per event,
/// cross-checked against /v4/matches/{matchId}/details: 1 = goal, 2 = own goal, 3 = assist,
/// 4 = yellow card, 8 = substituted in, 9 = substituted out.
/// </summary>
public class KickbaseTeamCenterPlayerDto
{
    [JsonPropertyName("i")]
    public required string Id { get; set; }

    [JsonPropertyName("n")]
    public required string Name { get; set; }

    [JsonPropertyName("p")]
    public int Points { get; set; }

    [JsonPropertyName("k")]
    public List<int> EventKeys { get; set; } = [];
}

public class KickbaseTeamCenterMatchDto
{
    [JsonPropertyName("mi")]
    public required string MatchId { get; set; }

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

public class KickbaseTeamCenterDto
{
    [JsonPropertyName("tid")]
    public required string TeamId { get; set; }

    [JsonPropertyName("tn")]
    public required string TeamName { get; set; }

    [JsonPropertyName("p")]
    public int Points { get; set; }

    [JsonPropertyName("ma")]
    public KickbaseTeamCenterMatchDto? Match { get; set; }

    [JsonPropertyName("it")]
    public List<KickbaseTeamCenterPlayerDto> Players { get; set; } = [];
}
