using System.Net.Http.Headers;

namespace KickbaseAssistant.Infrastructure.Kickbase;

/// <summary>
/// Attaches the current Kickbase bearer token to every outgoing request and retries
/// once with a freshly fetched token if the server responds with 401.
/// </summary>
public class KickbaseAuthHandler(KickbaseAuthService authService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var token = await authService.GetValidTokenAsync(ct);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, ct);
        if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        var refreshedToken = await authService.GetValidTokenAsync(ct);
        request = CloneRequest(request, refreshedToken);
        return await base.SendAsync(request, ct);
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage original, string token)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Content = original.Content,
        };
        clone.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return clone;
    }
}
