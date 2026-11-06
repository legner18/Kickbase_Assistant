namespace KickbaseAssistant.Infrastructure.Odds;

public class OddsOptions
{
    public const string SectionName = "Odds";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.the-odds-api.com";
    public string SportKey { get; set; } = "soccer_germany_bundesliga";
    public string Regions { get; set; } = "eu";
    public string CompetitionId { get; set; } = "1";
}
