using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KickbaseAssistant.Infrastructure.Notifications;

/// <summary>
/// Sends push notifications via ntfy.sh (https://ntfy.sh/docs/publish/#publish-as-json). Uses the
/// JSON publish endpoint (not the plain-text one) so German umlauts in titles/messages don't need
/// header-encoding workarounds. The topic name is treated as a secret (anyone who knows it can
/// subscribe or publish to it on the public ntfy.sh instance) - configured via NtfyOptions, never
/// hardcoded or committed.
/// </summary>
public class NtfyPushService(HttpClient httpClient, IOptions<NtfyOptions> options, ILogger<NtfyPushService> logger)
{
    public async Task SendAsync(string title, string message, string? tags = null, CancellationToken ct = default)
    {
        var topic = options.Value.Topic;
        if (string.IsNullOrWhiteSpace(topic))
        {
            logger.LogWarning("Ntfy:Topic ist nicht konfiguriert - Push wird übersprungen: {Title}", title);
            return;
        }

        var payload = new
        {
            topic,
            title,
            message,
            tags = tags is null ? null : tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
        };

        using var response = await httpClient.PostAsJsonAsync(options.Value.BaseUrl, payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Ntfy-Push fehlgeschlagen ({Status}): {Title}", response.StatusCode, title);
        }
    }
}
