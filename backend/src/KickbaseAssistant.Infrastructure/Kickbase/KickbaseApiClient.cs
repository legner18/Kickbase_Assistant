using System.Net;
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

    public async Task<IReadOnlyList<KickbaseActivityItemDto>> GetActivitiesFeedAsync(string leagueId, CancellationToken ct = default) =>
        (await GetAsync<KickbaseActivityFeedResponseDto>($"/v4/leagues/{leagueId}/activitiesFeed", ct)).Activities;

    public Task<KickbaseManagerTeamCenterDto> GetManagerTeamCenterAsync(
        string leagueId, string userId, int dayNumber, CancellationToken ct = default) =>
        GetAsync<KickbaseManagerTeamCenterDto>($"/v4/leagues/{leagueId}/users/{userId}/teamcenter?dayNumber={dayNumber}", ct);

    public Task<KickbasePlayerCenterDto> GetPlayerCenterAsync(
        string competitionId, string playerId, int day, CancellationToken ct = default) =>
        GetAsync<KickbasePlayerCenterDto>($"/v4/competitions/{competitionId}/playercenter/{playerId}?day={day}", ct);

    public async Task<IReadOnlyList<KickbaseEventTypeDto>> GetEventTypesAsync(CancellationToken ct = default) =>
        (await GetAsync<KickbaseEventTypesResponseDto>("/v4/live/eventtypes", ct, acceptLanguage: "de-DE")).Types;

    public static bool IsBlockedResponse(Exception exception) =>
        exception is HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden };

    public Task<KickbaseTeamCenterDto> GetCompetitionTeamCenterAsync(
        string competitionId, string teamId, int day, CancellationToken ct = default) =>
        GetAsync<KickbaseTeamCenterDto>($"/v4/competitions/{competitionId}/teams/{teamId}/teamcenter?day={day}", ct);

    public Task<KickbaseTeamCenterDto> GetCompetitionTeamCenterForMatchdayAsync(
        string competitionId, string teamId, int dayNumber, CancellationToken ct = default) =>
        GetAsync<KickbaseTeamCenterDto>($"/v4/competitions/{competitionId}/teams/{teamId}/teamcenter?dayNumber={dayNumber}", ct);

    public async Task<KickbaseTeamProfileDto> GetTeamProfileAsync(
        string competitionId, string teamId, CancellationToken ct = default) =>
        KickbaseTeamProfileDto.Parse(await GetJsonAsync($"/v4/competitions/{competitionId}/teams/{teamId}/teamprofile", ct));

    public Task<KickbaseRankingDto> GetRankingAsync(string leagueId, CancellationToken ct = default) =>
        GetAsync<KickbaseRankingDto>($"/v4/leagues/{leagueId}/ranking", ct);

    /// <summary>Only works for the logged-in user - see KickbaseBudgetDto.</summary>
    public Task<KickbaseBudgetDto> GetMyBudgetAsync(string leagueId, CancellationToken ct = default) =>
        GetAsync<KickbaseBudgetDto>($"/v4/leagues/{leagueId}/me/budget", ct);

    /// <summary>
    /// Paginiert vollständig durch - live verifiziert am 2026-10-11: die Antwort hat kein
    /// Gesamtzahl-/hasMore-Feld, aber "start" als Offset-Parameter funktioniert (im Gegensatz zu
    /// "offset"/"page"/"skip"/"from", die alle still ignoriert werden und wieder Seite 1 liefern).
    /// Seitengröße 25, erkannt am ersten Rückgabe-Batch mit weniger als 25 Einträgen. Ohne diese
    /// Paginierung lieferte der Aufruf bei einem Manager mit 66 echten Transfers nur die neuesten 25.
    /// </summary>
    public async Task<KickbaseManagerTransfersResponseDto> GetManagerTransfersAsync(
        string leagueId, string managerId, CancellationToken ct = default)
    {
        const int PageSize = 25;
        const int MaxPages = 40; // Sicherheitsgrenze (1000 Transfers) gegen eine Endlosschleife bei unerwartetem API-Verhalten.

        var allTransfers = new List<KickbaseManagerTransferDto>();
        string? managerName = null;
        var start = 0;

        for (var page = 0; page < MaxPages; page++)
        {
            var response = await GetAsync<KickbaseManagerTransfersResponseDto>(
                $"/v4/leagues/{leagueId}/managers/{managerId}/transfer?start={start}", ct);
            managerName ??= response.ManagerName;
            allTransfers.AddRange(response.Transfers);

            if (response.Transfers.Count < PageSize)
            {
                break;
            }

            start += PageSize;
        }

        return new KickbaseManagerTransfersResponseDto
        {
            ManagerId = managerId,
            ManagerName = managerName ?? "",
            Transfers = allTransfers,
        };
    }

    public async Task<IReadOnlyList<KickbaseManagerSquadPlayerDto>> GetManagerSquadAsync(
        string leagueId, string managerId, CancellationToken ct = default) =>
        (await GetAsync<KickbaseManagerSquadResponseDto>($"/v4/leagues/{leagueId}/managers/{managerId}/squad", ct)).Players;

    public async Task<IReadOnlyList<KickbasePlayerPerformanceSeasonDto>> GetPlayerPerformanceAsync(
        string competitionId, string playerId, CancellationToken ct = default) =>
        (await GetAsync<KickbasePlayerPerformanceResponseDto>(
            $"/v4/competitions/{competitionId}/players/{playerId}/performance", ct)).Seasons;

    private async Task<JsonElement> GetJsonAsync(string requestUri, CancellationToken ct)
    {
        using var client = CreateClient();
        using var response = await client.GetAsync(requestUri, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.Clone();
    }

    private async Task<T> GetAsync<T>(string requestUri, CancellationToken ct, string? acceptLanguage = null)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        if (acceptLanguage is not null)
        {
            request.Headers.AcceptLanguage.ParseAdd(acceptLanguage);
        }

        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: ct)
            ?? throw new InvalidOperationException($"Kickbase-Antwort von {requestUri} war leer/null.");
    }
}
