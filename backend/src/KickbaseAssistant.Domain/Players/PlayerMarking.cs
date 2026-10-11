namespace KickbaseAssistant.Domain.Players;

public enum MarkingColor
{
    None,
    Green,
    Yellow,
    Red,
    Blue,
}

/// <summary>"Sicher" bis "ausgeschlossen" - eigene Einschätzung als Fallback, wenn keine externe
/// Quelle (LigaInsider) eine Startelf-Prognose liefert.</summary>
public enum LineupEstimate
{
    Sicher,
    Erwartet,
    Unsicher,
    Unwahrscheinlich,
    Ausgeschlossen,
}

/// <summary>
/// Umbauplan "Markierungen und Labels": Farbe, bis zu 3 freie Labels und eine eigene
/// Startelf-Einschätzung pro Spieler, ligagebunden. Ein Eintrag pro (Liga, Spieler) - Upsert statt
/// Historie, die Markierung ist der aktuelle Stand, keine Chronik.
/// </summary>
public class PlayerMarking
{
    public long Id { get; set; }
    public required string LeagueId { get; set; }
    public required string PlayerId { get; set; }
    public MarkingColor Color { get; set; } = MarkingColor.None;
    public string[] Labels { get; set; } = [];
    public LineupEstimate? OwnLineupEstimate { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
