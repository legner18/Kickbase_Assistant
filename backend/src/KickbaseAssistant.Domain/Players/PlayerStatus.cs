namespace KickbaseAssistant.Domain.Players;

/// <summary>
/// Labels für den von Kickbase gelieferten Status-Code ("st"). Live gegen die Bousching-Liga
/// verifiziert sind nur 0 (fit) und 2; weitere, in der Community kursierende Codes sind nicht
/// bestätigt und fallen deshalb auf einen generischen "auffällig"-Text statt einer vermutlich
/// falschen konkreten Diagnose zurück.
/// </summary>
public static class PlayerStatus
{
    public const int Fit = 0;
    public const int Verletzt = 2;

    public static string GetLabel(int statusCode) => statusCode switch
    {
        Fit => "Fit",
        Verletzt => "Verletzt",
        _ => $"Auffällig (Code {statusCode})",
    };
}
