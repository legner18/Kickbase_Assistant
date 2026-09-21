using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace KickbaseAssistant.Infrastructure.LigaInsider;

public enum LigaInsiderLineupStatus
{
    Starting,
    Doubtful,
    Out,
    Bench,
}

public record LigaInsiderPlayerStatus(string Name, LigaInsiderLineupStatus Status, string? RawIconLabel);

/// <summary>
/// Scrapes a team's "voraussichtliche Aufstellung" page (ligainsider.de/{slug}/{id}/), confirmed
/// live on 2026-09-21. LigaInsider has no API and no ToS-defined rate limit, but the site's own
/// robots.txt allows crawling; still, callers should keep this to a few requests per team per
/// day (see Phase 4 in the project plan) - private use only, results must not be republished
/// publicly (LigaInsider's own content, not ours to redistribute).
///
/// DOM shape per formation slot (div.player_position_column): one or more div.sub_child
/// elements in document order. The first is the currently favored starter for that slot; any
/// further ones are alternates (hidden via inline style, shown via a "flip" button on the real
/// site) - typically the player they'd replace if doubtful/injured. A div.bottom_icon > img
/// with a non-empty "alt" attribute carries the status label (e.g. "Angeschlagen"); no icon at
/// all means no flag.
/// </summary>
public class LigaInsiderClient(IHttpClientFactory httpClientFactory, ILogger<LigaInsiderClient> logger)
{
    private static readonly Dictionary<string, LigaInsiderLineupStatus> IconLabelToStatus =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Angeschlagen"] = LigaInsiderLineupStatus.Doubtful,
            ["Fraglich"] = LigaInsiderLineupStatus.Doubtful,
            ["Rotationsgefahr"] = LigaInsiderLineupStatus.Doubtful,
            ["Verletzt"] = LigaInsiderLineupStatus.Out,
            ["Verletzung"] = LigaInsiderLineupStatus.Out,
            ["Gesperrt"] = LigaInsiderLineupStatus.Out,
            ["Aufbautraining"] = LigaInsiderLineupStatus.Out,
        };

    public async Task<IReadOnlyList<LigaInsiderPlayerStatus>> GetPredictedLineupAsync(
        string teamSlug, string teamId, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient(LigaInsiderHttpClientNames.Default);
        var html = await client.GetStringAsync($"/{teamSlug}/{teamId}/", ct);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var results = new List<LigaInsiderPlayerStatus>();
        var columns = doc.DocumentNode.SelectNodes(
            "//div[contains(concat(' ', normalize-space(@class), ' '), ' player_position_column ')]");
        if (columns is null)
        {
            logger.LogWarning(
                "LigaInsider: keine Aufstellungs-Slots gefunden für {Slug}/{Id} - Seitenstruktur hat sich vermutlich geändert.",
                teamSlug, teamId);
            return results;
        }

        foreach (var column in columns)
        {
            var subChildren = column.SelectNodes(
                ".//div[contains(concat(' ', normalize-space(@class), ' '), ' sub_child ')]");
            if (subChildren is null)
            {
                continue;
            }

            for (var i = 0; i < subChildren.Count; i++)
            {
                var nameNode = subChildren[i].SelectSingleNode(
                    ".//div[contains(@class,'player_name')]//a[@itemprop='athlete']");
                var name = nameNode?.InnerText.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var iconNode = subChildren[i].SelectSingleNode(".//div[contains(@class,'bottom_icon')]//img");
                var iconLabel = iconNode?.GetAttributeValue("alt", "").Trim();

                LigaInsiderLineupStatus status;
                if (!string.IsNullOrEmpty(iconLabel))
                {
                    if (!IconLabelToStatus.TryGetValue(iconLabel, out status))
                    {
                        logger.LogWarning(
                            "LigaInsider: unbekanntes Status-Icon {Label} bei {Name} ({Slug}) - vorsorglich als Fraglich eingestuft.",
                            iconLabel, name, teamSlug);
                        status = LigaInsiderLineupStatus.Doubtful;
                    }
                }
                else
                {
                    status = i == 0 ? LigaInsiderLineupStatus.Starting : LigaInsiderLineupStatus.Bench;
                }

                results.Add(new LigaInsiderPlayerStatus(HtmlEntity.DeEntitize(name) ?? name, status, iconLabel));
            }
        }

        return results;
    }
}
