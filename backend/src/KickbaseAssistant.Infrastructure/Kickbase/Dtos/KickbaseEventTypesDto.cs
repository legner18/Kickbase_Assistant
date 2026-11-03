using System.Text.Json.Serialization;

namespace KickbaseAssistant.Infrastructure.Kickbase.Dtos;

/// <summary>
/// Field names confirmed live against GET /v4/live/eventtypes on 2026-10-29: 621 types, titles follow
/// Accept-Language (de-DE returns German titles). Not every "eti" seen in playercenter events is listed
/// (e.g. -20).
/// </summary>
public class KickbaseEventTypeDto
{
    [JsonPropertyName("i")]
    public int Id { get; set; }

    [JsonPropertyName("ti")]
    public required string Title { get; set; }
}

public class KickbaseEventTypesResponseDto
{
    [JsonPropertyName("it")]
    public List<KickbaseEventTypeDto> Types { get; set; } = [];
}
