using KickbaseAssistant.Domain.Players;
using KickbaseAssistant.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KickbaseAssistant.Worker.Jobs;

/// <summary>
/// Shared dedup rule for SellAlerts, used by both LigaInsiderScrapeJob (LineupDowngrade) and
/// SellAlertJob (MarketValueDrop, InjuryNews): only ever one *open* (unacknowledged) alert per
/// player+signal at a time. This naturally caps pushes to one per problem episode - no daily
/// spam for an ongoing issue, and a fresh alert (with a fresh push) only fires again once the
/// user has acknowledged the previous one and the condition re-triggers.
/// </summary>
public static class SellAlertHelper
{
    public static async Task<bool> TryCreateAsync(
        AppDbContext dbContext, string playerId, SellAlertSignal signal, string reason, CancellationToken ct)
    {
        var alreadyOpen = await dbContext.SellAlerts
            .AnyAsync(a => a.PlayerId == playerId && a.Signal == signal && !a.Acknowledged, ct);
        if (alreadyOpen)
        {
            return false;
        }

        dbContext.SellAlerts.Add(new SellAlert
        {
            PlayerId = playerId,
            Signal = signal,
            Reason = reason,
            CreatedAt = DateTimeOffset.UtcNow,
            Acknowledged = false,
        });
        return true;
    }
}
