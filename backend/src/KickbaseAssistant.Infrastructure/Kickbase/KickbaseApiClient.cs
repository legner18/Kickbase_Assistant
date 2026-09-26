using System.Text.Json;
using KickbaseAssistant.Infrastructure.Kickbase.Dtos;

namespace KickbaseAssistant.Infrastructure.Kickbase;

/// <summary>
/// Thin wrapper around the unofficial Kickbase API v4. Endpoint paths are taken from the
/// community-maintained spec at github.com/kevinskyba/kickbase-api-doc (as of 2026-03-06).
/// GetMeAsync/GetLeaguesAsync/GetSquadAsync are typed against field names confirmed live on
/// 2026-09-21 (see Dtos/). The remaining endpoints still return raw <see cref="JsonElement"/>
/// because their field names are AI-paraphrased in the community spec and not yet confirmed
/// against a live account - probe them the same way before building typed DTOs on top.
/// </summary>
public class KickbaseApiClient(IHttpClientFactory httpClientFactory)
{
    private HttpClient CreateClient() => httpClientFactory.CreateClient(KickbaseHttpClientNames.Authenticated);

    public async Task<KickbaseUserDto> GetMeAsync(CancellationToken ct = default) =>
        (await GetAsync<KickbaseMeResponseDto>("/v4/user/me", ct)).User;

    public async Task<IReadOnlyList<KickbaseLeagueDto>> GetLeaguesAsync(CancellationToken ct = default) =>
        (await GetAsync<KickbaseLeaguesResponseDto>("/v4/leagues", ct)).Leagues;

    public async Task<IReadOnlyList<KickbaseSquadPlayerDto>> GetSquadAsync(string leagueId, CancellationToken ct = default) =>
        (await GetAsync<KickbaseSquadResponseDto>($"/v4/leagues/{leagueId}/squad", ct)).Players;

    public async Task<IReadOnlyList<KickbaseTeamDto>> GetCompetitionTableAsync(string competitionId, CancellationToken ct = default) =>
        (await GetAsync<KickbaseTableResponseDto>($"/v4/competitions/{competitionId}/table", ct)).Teams;

    public async Task<IReadOnlyList<KickbaseNewsItemDto>> GetNewsAsync(string competitionId, CancellationToken ct = default) =>
        (await GetAsync<KickbaseNewsResponseDto>($"/v4/base/news/{competitionId}", ct)).Items;

    /// <summary>Escape hatch for endpoints not yet mapped to a typed DTO - probe first, then add a real method.</summary>
    public Task<JsonElement> GetRawAsync(string path, CancellationToken ct = default) =>
        GetJsonAsync(path, ct);

    public async Task<IReadOnlyList<KickbaseMarketPlayerDto>> GetMarketAsync(string leagueId, CancellationToken ct = default) =>
        (await GetAsync<KickbaseMarketResponseDto>($"/v4/leagues/{leagueId}/market", ct)).Players;

    /// <summary>timeframe must be 92 (3 months) or 365 (1 year) - see KickbaseMarketValueHistoryDto.</summary>
    public async Task<IReadOnlyList<KickbaseMarketValueHistoryPointDto>> GetMarketValueHistoryAsync(
        string competitionId, string playerId, int timeframe, CancellationToken ct = default) =>
        (await GetAsync<KickbaseMarketValueHistoryResponseDto>(
            $"/v4/competitions/{competitionId}/players/{playerId}/marketvalue/{timeframe}", ct)).Points;

    public async Task<IReadOnlyList<KickbaseMatchdayDto>> GetMatchdaysAsync(string competitionId, CancellationToken ct = default) =>
        (await GetAsync<KickbaseMatchdaysResponseDto>($"/v4/competitions/{competitionId}/matchdays", ct)).Matchdays;

    public Task<JsonElement> GetActivitiesFeedAsync(string leagueId, CancellationToken ct = default) =>
        GetJsonAsync($"/v4/leagues/{leagueId}/activitiesFeed", ct);

    public Task<JsonElement> GetTeamCenterAsync(string leagueId, string userId, int dayNumber, CancellationToken ct = default) =>
        GetJsonAsync($"/v4/leagues/{leagueId}/users/{userId}/teamcenter?dayNumber={dayNumber}", ct);

    public Task<KickbaseRankingDto> GetRankingAsync(string leagueId, CancellationToken ct = default) =>
        GetAsync<KickbaseRankingDto>($"/v4/leagues/{leagueId}/ranking", ct);

    /// <summary>Only works for the logged-in user - see KickbaseBudgetDto.</summary>
    public Task<KickbaseBudgetDto> GetMyBudgetAsync(string leagueId, CancellationToken ct = default) =>
        GetAsync<KickbaseBudgetDto>($"/v4/leagues/{leagueId}/me/budget", ct);

    public Task<KickbaseManagerTransfersResponseDto> GetManagerTransfersAsync(
        string leagueId, string managerId, CancellationToken ct = default) =>
        GetAsync<KickbaseManagerTransfersResponseDto>($"/v4/leagues/{leagueId}/managers/{managerId}/transfer", ct);

    public async Task<IReadOnlyList<KickbaseManagerSquadPlayerDto>> GetManagerSquadAsync(
        string leagueId, string managerId, CancellationToken ct = default) =>
        (await GetAsync<KickbaseManagerSquadResponseDto>($"/v4/leagues/{leagueId}/managers/{managerId}/squad", ct)).Players;

    private async Task<JsonElement> GetJsonAsync(string requestUri, CancellationToken ct)
    {
        using var client = CreateClient();
        using var response = await client.GetAsync(requestUri, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.Clone();
    }

    private async Task<T> GetAsync<T>(string requestUri, CancellationToken ct)
    {
        using var client = CreateClient();
        using var response = await client.GetAsync(requestUri, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: ct)
            ?? throw new InvalidOperationException($"Kickbase-Antwort von {requestUri} war leer/null.");
    }
}
