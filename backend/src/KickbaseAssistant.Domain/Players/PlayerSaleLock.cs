namespace KickbaseAssistant.Domain.Players;

/// <summary>
/// Umbauplan "Verkaufsplaner", Nebenbedingung 3: "Spieler mit fixem Haken 'nicht verkaufen'
/// bleiben drin". Bewusst minimal (nur Existenz = gesperrt) statt der vollen "Markierungen und
/// Labels" aus Phase 3 (Farben, freie Labels, Ablaufzeit) - die braucht der Verkaufsplaner nicht.
/// </summary>
public class PlayerSaleLock
{
    public long Id { get; set; }
    public required string LeagueId { get; set; }
    public required string PlayerId { get; set; }
}
