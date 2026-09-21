namespace KickbaseAssistant.Infrastructure.Kickbase;

public class KickbaseOptions
{
    public const string SectionName = "Kickbase";

    public string BaseUrl { get; set; } = "https://api.kickbase.com";
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
