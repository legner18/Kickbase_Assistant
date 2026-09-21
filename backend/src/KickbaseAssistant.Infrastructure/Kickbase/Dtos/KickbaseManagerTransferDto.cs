using System.Text.Json.Serialization;

namespace KickbaseAssistant.Infrastructure.Kickbase.Dtos;

/// <summary>
/// Field names confirmed live against GET /v4/leagues/{leagueId}/managers/{managerId}/transfer
/// on 2026-09-21. "tty" (transfer type) is 1 for a buy and 2 for a sell - confirmed by comparing
/// timestamps of a buy/sell pair for the same player in a real response (the tty=1 entry always
/// precedes the tty=2 entry chronologically). "othnm" is only present for direct manager-to-
/// manager trades - absent when the other side was the open market.
/// </summary>
public class KickbaseManagerTransferDto
{
    [JsonPropertyName("pi")]
    public required string PlayerId { get; set; }

    [JsonPropertyName("pn")]
    public required string PlayerName { get; set; }

    [JsonPropertyName("tid")]
    public required string TeamId { get; set; }

    [JsonPropertyName("tty")]
    public int TransferTypeRaw { get; set; }

    [JsonPropertyName("trp")]
    public int Price { get; set; }

    [JsonPropertyName("dt")]
    public DateTimeOffset OccurredAt { get; set; }

    [JsonPropertyName("othnm")]
    public string? OtherManagerName { get; set; }

    public bool IsBuy => TransferTypeRaw == 1;
}

public class KickbaseManagerTransfersResponseDto
{
    [JsonPropertyName("u")]
    public required string ManagerId { get; set; }

    [JsonPropertyName("unm")]
    public required string ManagerName { get; set; }

    [JsonPropertyName("it")]
    public required List<KickbaseManagerTransferDto> Transfers { get; set; }
}
