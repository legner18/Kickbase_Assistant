using System.Text.Json.Serialization;

namespace KickbaseAssistant.Infrastructure.Kickbase.Dtos;

/// <summary>
/// Field names confirmed live against GET /v4/competitions/{competitionId}/playercenter/{playerId}?day={day}
/// on 2026-10-29 (finished match). "events" holds every point event of the matchday - for the sample
/// player the 98 "p" values summed up exactly to the top-level "p" (365). Corrections arrive as separate
/// events with negative "p". "eti" refers to /v4/live/eventtypes. Match-flow markers come with "eti" <= 0,
/// "p" 0 and a "ke" (10 kickoff, 11 second half, 12 half time, 13 full time, 26 added time).
/// </summary>
public class KickbasePlayerCenterEventDto
{
    [JsonPropertyName("ei")]
    public required string Id { get; set; }

    [JsonPropertyName("eti")]
    public int EventTypeId { get; set; }

    [JsonPropertyName("p")]
    public int Points { get; set; }

    [JsonPropertyName("mt")]
    public int Minute { get; set; }

    [JsonPropertyName("ke")]
    public int? KeyEvent { get; set; }
}

public class KickbasePlayerCenterDto
{
    [JsonPropertyName("p")]
    public int Points { get; set; }

    [JsonPropertyName("events")]
    public List<KickbasePlayerCenterEventDto> Events { get; set; } = [];
}
