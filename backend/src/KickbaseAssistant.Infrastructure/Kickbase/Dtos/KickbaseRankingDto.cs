using System.Text.Json.Serialization;

namespace KickbaseAssistant.Infrastructure.Kickbase.Dtos;

/// <summary>
/// Field names confirmed live against GET /v4/leagues/{leagueId}/ranking on 2026-09-21. "il"
/// (is live) at the top level is the cheapest signal for "is a match live right now" - no need
/// to cross-reference matchday kickoff times for that. Note: /v4/live/eventtypes only lists
/// event names ("Cross", "Goal", ...), not point values, so Kickbase's scoring formula isn't
/// reconstructable client-side - "mdp" (matchday points) here is the authoritative, already-
/// computed number straight from Kickbase, which is what Phase 6 polls instead.
/// </summary>
public class KickbaseRankingDto
{
    [JsonPropertyName("il")]
    public bool IsLive { get; set; }

    [JsonPropertyName("day")]
    public int CurrentMatchday { get; set; }

    [JsonPropertyName("us")]
    public required List<KickbaseRankingUserDto> Users { get; set; }
}

public class KickbaseRankingUserDto
{
    [JsonPropertyName("i")]
    public required string UserId { get; set; }

    [JsonPropertyName("n")]
    public required string Name { get; set; }

    [JsonPropertyName("sp")]
    public int SeasonPoints { get; set; }

    [JsonPropertyName("mdp")]
    public int MatchdayPoints { get; set; }

    [JsonPropertyName("spl")]
    public int SeasonRank { get; set; }

    [JsonPropertyName("mdpl")]
    public int MatchdayRank { get; set; }

    /// <summary>Teamwert (Summe Marktwerte des Kaders), von Kickbase selbst berechnet - live
    /// verifiziert am 2026-10-11, inkl. Nachkommastellen (Double statt Int wie bei Player.MarketValue,
    /// Vorsicht beim Vergleich/Runden).</summary>
    [JsonPropertyName("tv")]
    public double TeamValue { get; set; }
}
