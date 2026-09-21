using System.Text.Json.Serialization;

namespace KickbaseAssistant.Infrastructure.Kickbase.Dtos;

/// <summary>
/// Field names confirmed live against GET /v4/base/news/{competitionId} on 2026-09-21. This is a
/// general editorial/marketing feed for the whole competition (MVP shoutouts, ads, challenges),
/// not a clean per-player injury feed - only some items are about a specific player and/or tagged
/// "Sperren und Verletzungen" (suspensions/injuries) via "rl". When "url" points at
/// kickbase://competition/{id}/players/{playerId}/profile, PlayerId below is extracted from it -
/// that's a much more reliable player link than matching on free text.
/// </summary>
public class KickbaseNewsItemDto
{
    [JsonPropertyName("i")]
    public required string Id { get; set; }

    [JsonPropertyName("ti")]
    public required string Title { get; set; }

    [JsonPropertyName("rl")]
    public string? Subtitle { get; set; }

    [JsonPropertyName("d")]
    public string? Description { get; set; }

    [JsonPropertyName("dt")]
    public DateTimeOffset PublishedAt { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    public string? PlayerId
    {
        get
        {
            if (Url is null)
            {
                return null;
            }

            var marker = "/players/";
            var start = Url.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                return null;
            }

            start += marker.Length;
            var end = Url.IndexOf('/', start);
            return end < 0 ? Url[start..] : Url[start..end];
        }
    }
}

public class KickbaseNewsResponseDto
{
    [JsonPropertyName("it")]
    public required List<KickbaseNewsItemDto> Items { get; set; }
}
