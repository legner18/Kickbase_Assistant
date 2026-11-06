using System.Text.Json;
using Microsoft.Extensions.Options;

namespace KickbaseAssistant.Infrastructure.Odds;

public record BookmakerOdds(string Bookmaker, double Home, double Draw, double Away);

public record OddsEvent(string HomeTeam, string AwayTeam, DateTimeOffset CommenceTime, IReadOnlyList<BookmakerOdds> Bookmakers);

public record OddsResponse(IReadOnlyList<OddsEvent> Events, string? RemainingRequests);

/// <summary>
/// The Odds API v4 (https://the-odds-api.com/liveapi/guides/v4/), Markt "h2h" mit Dezimalquoten. Wie
/// das Unentschieden-Outcome heißt, nennt die Doku nicht - es wird deshalb als das Outcome erkannt,
/// das weder Heim- noch Auswärtsteam ist.
/// </summary>
public class OddsApiClient(HttpClient httpClient, IOptions<OddsOptions> options)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    public async Task<OddsResponse> GetOddsAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var requestUri = $"{settings.BaseUrl.TrimEnd('/')}/v4/sports/{settings.SportKey}/odds/"
            + $"?apiKey={Uri.EscapeDataString(settings.ApiKey)}&regions={Uri.EscapeDataString(settings.Regions)}"
            + "&markets=h2h&oddsFormat=decimal&dateFormat=iso";

        using var response = await httpClient.GetAsync(requestUri, ct);
        response.EnsureSuccessStatusCode();

        var remainingRequests = response.Headers.TryGetValues("x-requests-remaining", out var values)
            ? values.FirstOrDefault()
            : null;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return new OddsResponse(ParseEvents(document.RootElement), remainingRequests);
    }

    private static List<OddsEvent> ParseEvents(JsonElement root)
    {
        var events = new List<OddsEvent>();
        if (root.ValueKind != JsonValueKind.Array)
        {
            return events;
        }

        foreach (var item in root.EnumerateArray())
        {
            var homeTeam = ReadString(item, "home_team");
            var awayTeam = ReadString(item, "away_team");
            var commenceTime = ReadString(item, "commence_time");
            if (homeTeam is null || awayTeam is null || !DateTimeOffset.TryParse(commenceTime, out var kickoff))
            {
                continue;
            }

            var bookmakers = new List<BookmakerOdds>();
            if (item.TryGetProperty("bookmakers", out var bookmakerItems) && bookmakerItems.ValueKind == JsonValueKind.Array)
            {
                foreach (var bookmaker in bookmakerItems.EnumerateArray())
                {
                    var odds = ReadHeadToHead(bookmaker, homeTeam, awayTeam);
                    if (odds is not null)
                    {
                        bookmakers.Add(odds);
                    }
                }
            }

            events.Add(new OddsEvent(homeTeam, awayTeam, kickoff, bookmakers));
        }

        return events;
    }

    private static BookmakerOdds? ReadHeadToHead(JsonElement bookmaker, string homeTeam, string awayTeam)
    {
        if (!bookmaker.TryGetProperty("markets", out var markets) || markets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var market in markets.EnumerateArray())
        {
            if (ReadString(market, "key") != "h2h"
                || !market.TryGetProperty("outcomes", out var outcomes)
                || outcomes.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            double? home = null;
            double? away = null;
            var others = new List<double>();
            foreach (var outcome in outcomes.EnumerateArray())
            {
                var name = ReadString(outcome, "name");
                if (!outcome.TryGetProperty("price", out var priceElement) || !priceElement.TryGetDouble(out var price))
                {
                    continue;
                }

                if (name == homeTeam)
                {
                    home = price;
                }
                else if (name == awayTeam)
                {
                    away = price;
                }
                else
                {
                    others.Add(price);
                }
            }

            if (home is { } homePrice && away is { } awayPrice && others.Count == 1)
            {
                return new BookmakerOdds(ReadString(bookmaker, "key") ?? "unbekannt", homePrice, others[0], awayPrice);
            }
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
