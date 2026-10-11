namespace KickbaseAssistant.Domain.Leagues;

/// <summary>
/// Zuletzt von BalanceGuardJob berechneter Kontostand-Wächter-Status je Liga - eine Zeile pro
/// Liga, stündlich überschrieben (kein Verlauf nötig, nur "wie steht's gerade"). Die Api liest
/// das nur aus der DB, ruft Kickbase selbst nie direkt auf (das bleibt Sache des Workers).
/// </summary>
public class LeagueBalanceStatus
{
    public long Id { get; set; }
    public required string LeagueId { get; set; }
    public double Balance { get; set; }
    public double CreditLimit { get; set; }
    public BalanceStatus Status { get; set; }
    public int NextMatchdayNumber { get; set; }
    public DateTimeOffset NextKickoffAt { get; set; }
    public string[] RosterGaps { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; }
}
