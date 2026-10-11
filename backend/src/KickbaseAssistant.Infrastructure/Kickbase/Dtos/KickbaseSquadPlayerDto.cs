using System.Text.Json.Serialization;

namespace KickbaseAssistant.Infrastructure.Kickbase.Dtos;

/// <summary>
/// Field names confirmed live against GET /v4/leagues/{leagueId}/squad on 2026-09-21. Only the
/// fields Phase 2 (Marktwert-Tracker) needs are mapped; the raw response has more (status flags,
/// lineup order, trend deltas) that later phases (Aufstellungs-Wächter, Verkaufs-Alarm) can add.
///
/// "p"/"ap" (total/average points) added 2026-09-26 - same field names already confirmed live on
/// the market ("p"/"ap" in KickbaseMarketPlayerDto) and other-manager-squad ("p"/"ap" in
/// KickbaseManagerSquadPlayerDto) endpoints; this squad endpoint was the one place points were
/// never mapped, which silently left the user's own squad stuck at 0 points in the DB forever.
///
/// "st" (status) added 2026-10-11, live-verified against the Bousching squad (14 players with
/// st=0, one with st=2 and stl=[2]). Only 0 ("fit") and 2 are actually confirmed; other codes
/// occasionally cited in community write-ups (1, 4, 8, 16 for angeschlagen/rot/aufbautraining/
/// gelb-rot) are unverified for this API version - see PlayerStatus.GetLabel, which falls back to
/// a generic label for anything not confirmed live.
/// </summary>
public class KickbaseSquadPlayerDto
{
    [JsonPropertyName("i")]
    public required string Id { get; set; }

    [JsonPropertyName("n")]
    public required string Name { get; set; }

    [JsonPropertyName("tid")]
    public required string TeamId { get; set; }

    [JsonPropertyName("pos")]
    public int Position { get; set; }

    [JsonPropertyName("mv")]
    public int MarketValue { get; set; }

    [JsonPropertyName("p")]
    public int TotalPoints { get; set; }

    [JsonPropertyName("ap")]
    public int AveragePoints { get; set; }

    [JsonPropertyName("st")]
    public int Status { get; set; }
}

public class KickbaseSquadResponseDto
{
    [JsonPropertyName("it")]
    public required List<KickbaseSquadPlayerDto> Players { get; set; }
}
