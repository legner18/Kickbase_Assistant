using System.Globalization;
using System.Text.Json;

namespace KickbaseAssistant.Infrastructure.Kickbase.Dtos;

/// <summary>
/// GET /v4/competitions/{competitionId}/teams/{teamId}/teamprofile - NICHT live verifiziert (Stand
/// 2026-11-05). Feldnamen sind wie beim Kader-Endpunkt angenommen ("it" mit "i", "n", "pos", "mv",
/// optional "ap", "p", "st"), darum bewusst von Hand aus dem JsonElement gelesen statt deserialisiert:
/// passt die Form nicht, kommt eine leere Liste plus die tatsächlichen Top-Level-Felder zurück, die
/// BundesligaMarketValueJob ins Log schreibt.
/// </summary>
public record KickbaseTeamProfilePlayerDto(
    string Id, string Name, int Position, int MarketValue, int? AveragePoints, int? TotalPoints, int? Status);

public record KickbaseTeamProfileDto(IReadOnlyList<KickbaseTeamProfilePlayerDto> Players, IReadOnlyList<string> TopLevelFields)
{
    public static KickbaseTeamProfileDto Parse(JsonElement root)
    {
        var topLevelFields = root.ValueKind == JsonValueKind.Object
            ? root.EnumerateObject().Select(property => property.Name).ToList()
            : [];

        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("it", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return new KickbaseTeamProfileDto([], topLevelFields);
        }

        var players = new List<KickbaseTeamProfilePlayerDto>();
        foreach (var item in items.EnumerateArray())
        {
            var id = ReadString(item, "i");
            var name = ReadString(item, "n");
            var position = ReadInt(item, "pos");
            var marketValue = ReadInt(item, "mv");
            if (id is null || name is null || position is null || marketValue is null)
            {
                continue;
            }

            players.Add(new KickbaseTeamProfilePlayerDto(
                id, name, position.Value, marketValue.Value, ReadInt(item, "ap"), ReadInt(item, "p"), ReadInt(item, "st")));
        }

        return new KickbaseTeamProfileDto(players, topLevelFields);
    }

    private static string? ReadString(JsonElement item, string propertyName)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }

    private static int? ReadInt(JsonElement item, string propertyName)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt32(out var whole) ? whole : (int)Math.Round(value.GetDouble()),
            JsonValueKind.String => int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null,
            _ => null,
        };
    }
}
