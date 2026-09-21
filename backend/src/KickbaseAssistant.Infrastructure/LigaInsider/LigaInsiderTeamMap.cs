namespace KickbaseAssistant.Infrastructure.LigaInsider;

/// <summary>
/// Kickbase and LigaInsider have their own, unrelated team-id schemes. Kickbase exposes a short
/// team name ("tn" on GET /v4/competitions/{id}/table, e.g. "Bayern") which this maps to
/// LigaInsider's team-page slug and numeric id (ligainsider.de/{Slug}/{Id}/), confirmed live on
/// 2026-09-21 against the current 1. Bundesliga season. Revisit this list after every
/// promotion/relegation (end of season).
/// </summary>
public static class LigaInsiderTeamMap
{
    private static readonly Dictionary<string, (string Slug, string Id)> ByKickbaseTeamName = new()
    {
        ["Bayern"] = ("fc-bayern-muenchen", "1"),
        ["Dortmund"] = ("borussia-dortmund", "14"),
        ["Leipzig"] = ("rb-leipzig", "1311"),
        ["Stuttgart"] = ("vfb-stuttgart", "12"),
        ["Hoffenheim"] = ("tsg-hoffenheim", "10"),
        ["Leverkusen"] = ("bayer-04-leverkusen", "4"),
        ["Freiburg"] = ("sc-freiburg", "18"),
        ["Frankfurt"] = ("eintracht-frankfurt", "3"),
        ["Augsburg"] = ("fc-augsburg", "21"),
        ["Mainz"] = ("1-fsv-mainz-05", "17"),
        ["Union Berlin"] = ("1-fc-union-berlin", "1246"),
        ["M'gladbach"] = ("borussia-moenchengladbach", "5"),
        ["Hamburg"] = ("hamburger-sv", "9"),
        ["Köln"] = ("1-fc-koeln", "15"),
        ["Bremen"] = ("sv-werder-bremen", "2"),
        ["Schalke"] = ("fc-schalke-04", "13"),
        ["Elversberg"] = ("sv-07-elversberg", "1331"),
        ["Paderborn"] = ("sc-paderborn-07", "1249"),
    };

    public static bool TryGetTeamPage(string kickbaseTeamName, out (string Slug, string Id) teamPage) =>
        ByKickbaseTeamName.TryGetValue(kickbaseTeamName, out teamPage);
}
