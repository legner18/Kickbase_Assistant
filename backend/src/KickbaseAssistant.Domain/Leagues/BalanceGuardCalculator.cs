namespace KickbaseAssistant.Domain.Leagues;

public enum BalanceStatus
{
    Green,
    Yellow,
    Red,
}

/// <summary>
/// Reine Rechenlogik für den Kontostand-Wächter (Umbauplan), bewusst ohne DB/HTTP-Abhängigkeiten
/// gehalten, damit sie sich mit festen Beispielkadern unit-testen lässt.
/// </summary>
public static class BalanceGuardCalculator
{
    /// <summary>
    /// Kickbases offizielle 33%-Regel (help.kickbase.com/help/wie-weit-darf-ich-ins-minus):
    /// der Kontostand darf maximal 33% des um das aktuelle Minus reduzierten Teamwerts unterschreiten.
    /// </summary>
    public static double CreditLimit(double teamValue, double balance)
    {
        var currentDebt = Math.Max(0, -balance);
        return 0.33 * (teamValue - currentDebt);
    }

    /// <summary>
    /// Grün: Kontostand bereits im Plus. Gelb: im Minus, aber ein Sicherheitsabschlag (70%) auf die
    /// Summe der positiven 3-Tage-Marktwert-Trends der eigenen Spieler würde reichen, um bis zum
    /// nächsten MW-Update ins Plus zu drehen. Rot: auch das reicht nicht.
    /// </summary>
    public static BalanceStatus GetStatus(double balance, double expectedPositiveBuffer)
    {
        if (balance >= 0)
        {
            return BalanceStatus.Green;
        }

        const double safetyDiscount = 0.7;
        return balance + safetyDiscount * expectedPositiveBuffer >= 0 ? BalanceStatus.Yellow : BalanceStatus.Red;
    }
}
