namespace KickbaseAssistant.Domain.Leagues;

public enum BalanceAlertWindow
{
    TwentyFourHour,
    ThreeHour,
}

/// <summary>
/// Dedup-Record für BalanceGuardJob: pro (Liga, Spieltag, Fenster) höchstens ein Alarm, damit der
/// stündliche Job nicht mehrfach für dasselbe Ereignis pusht. Anders als SellAlert kein
/// offen/quittiert-Zustand - das Ereignis "T-24h/T-3h vor Anpfiff erreicht" ist einmalig und
/// braucht keine Nutzer-Quittierung.
/// </summary>
public class BalanceAlertSent
{
    public long Id { get; set; }
    public required string LeagueId { get; set; }
    public int MatchdayNumber { get; set; }
    public BalanceAlertWindow Window { get; set; }
    public DateTimeOffset SentAt { get; set; }
}
