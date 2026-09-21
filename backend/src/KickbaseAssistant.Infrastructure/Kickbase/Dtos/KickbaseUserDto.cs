using System.Text.Json.Serialization;

namespace KickbaseAssistant.Infrastructure.Kickbase.Dtos;

/// <summary>
/// Field names confirmed live against GET /v4/user/me on 2026-09-21 (not documented in the
/// community OpenAPI spec, which has no schema section).
/// </summary>
public class KickbaseUserDto
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

public class KickbaseMeResponseDto
{
    [JsonPropertyName("u")]
    public required KickbaseUserDto User { get; set; }
}
