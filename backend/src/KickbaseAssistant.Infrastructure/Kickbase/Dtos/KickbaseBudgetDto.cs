using System.Text.Json.Serialization;

namespace KickbaseAssistant.Infrastructure.Kickbase.Dtos;

/// <summary>
/// Field names confirmed live against GET /v4/leagues/{leagueId}/me/budget on 2026-09-21. Only
/// works for the logged-in user - Kickbase doesn't expose other managers' exact budget, which is
/// why LeagueAnalysisJob derives a starting-budget constant from this and the user's own full
/// transfer history, then applies the same formula to every other manager's transfer history.
/// </summary>
public class KickbaseBudgetDto
{
    [JsonPropertyName("b")]
    public double Budget { get; set; }
}
