using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KickbaseAssistant.Infrastructure.Kickbase;

/// <summary>
/// Handles login against the unofficial Kickbase API and caches the resulting token
/// until it is close to expiry. Kickbase's login response field for the token has been
/// observed as either "token" or "tkn" depending on API version - both are checked.
/// </summary>
public class KickbaseAuthService(
    IHttpClientFactory httpClientFactory,
    IOptions<KickbaseOptions> options,
    ILogger<KickbaseAuthService> logger)
{
    private static readonly TimeSpan RefreshSafetyMargin = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan FallbackTokenLifetime = TimeSpan.FromHours(1);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAtUtc = DateTimeOffset.MinValue;

    public async Task<string> GetValidTokenAsync(CancellationToken ct = default)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _expiresAtUtc - RefreshSafetyMargin)
        {
            return _token;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAtUtc - RefreshSafetyMargin)
            {
                return _token;
            }

            return await LoginAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<string> LoginAsync(CancellationToken ct)
    {
        var kickbaseOptions = options.Value;
        if (string.IsNullOrWhiteSpace(kickbaseOptions.Email) || string.IsNullOrWhiteSpace(kickbaseOptions.Password))
        {
            throw new InvalidOperationException(
                "Kickbase-Zugangsdaten fehlen. Bitte Kickbase:Email und Kickbase:Password per User Secrets oder Umgebungsvariablen setzen.");
        }

        using var client = httpClientFactory.CreateClient(KickbaseHttpClientNames.Anonymous);

        using var response = await client.PostAsJsonAsync("/v4/user/login", new
        {
            em = kickbaseOptions.Email,
            pass = kickbaseOptions.Password,
        }, ct);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var token = TryGetToken(doc.RootElement)
            ?? throw new InvalidOperationException(
                "Kickbase-Login-Antwort enthielt kein erkennbares Token-Feld (weder 'token' noch 'tkn'). " +
                "Die inoffizielle API hat sich vermutlich geändert - Antwort-Payload prüfen.");

        _token = token;
        _expiresAtUtc = TryGetExpiryFromJwt(token) ?? DateTimeOffset.UtcNow + FallbackTokenLifetime;

        logger.LogInformation("Kickbase-Login erfolgreich, Token gültig bis {ExpiresAtUtc:O}", _expiresAtUtc);

        return _token;
    }

    private static string? TryGetToken(JsonElement root)
    {
        foreach (var fieldName in new[] { "token", "tkn" })
        {
            if (root.TryGetProperty(fieldName, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static DateTimeOffset? TryGetExpiryFromJwt(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2)
            {
                return null;
            }

            var payload = parts[1];
            var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var bytes = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(bytes));

            if (doc.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var expUnix))
            {
                return DateTimeOffset.FromUnixTimeSeconds(expUnix);
            }
        }
        catch (Exception)
        {
            // JWT-Struktur unbekannt/inkompatibel - Fallback-Lebensdauer wird verwendet.
        }

        return null;
    }
}
