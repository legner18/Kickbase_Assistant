namespace KickbaseAssistant.Infrastructure.Kickbase;

public class KickbaseOptions
{
    public const string SectionName = "Kickbase";

    public string BaseUrl { get; set; } = "https://api.kickbase.com";
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Optional: beschränkt die ganze App auf genau diese eine Liga (z.B. "14612846" = Bousching).
    /// LeagueSyncService behandelt dann jede andere Liga des Accounts so, als wäre der Account dort
    /// nicht Mitglied - sie wird archiviert (IsActive = false), und da alle Jobs nur aktive Ligen
    /// verarbeiten, ruft danach nichts mehr Kickbase für diese Ligen auf. Leer = alle Ligen.
    /// </summary>
    public string? OnlyLeagueId { get; set; }
}
