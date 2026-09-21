namespace KickbaseAssistant.Infrastructure.Notifications;

public class NtfyOptions
{
    public const string SectionName = "Ntfy";

    public string BaseUrl { get; set; } = "https://ntfy.sh";
    public string Topic { get; set; } = string.Empty;
}
