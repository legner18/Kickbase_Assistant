using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Kickbase;
using KickbaseAssistant.Infrastructure.Kickbase.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KickbaseAssistant.Infrastructure.Leagues;

/// <summary>
/// Liga-Sync (Umbauplan Phase 1): hält die Leagues-Tabelle mit dem Kickbase-Account in Sync.
/// Neue Ligen werden angelegt, bekannte Ligen aktualisiert, und Ligen, die der Account nicht mehr
/// liefert, werden archiviert (IsActive = false) statt gelöscht, damit ihre Historie für spätere
/// Auswertungen erhalten bleibt. Alle Jobs filtern bereits auf IsActive, Archivierung stoppt also
/// automatisch jede weitere Verarbeitung für diese Liga.
///
/// Wird sowohl vom eigenständigen, stündlichen LeagueSyncJob aufgerufen als auch von
/// MarketValueSnapshotJob (das vorher dieselbe Logik inline hatte), damit der abendliche
/// Snapshot-Lauf nicht auf den nächsten Stunden-Tick warten muss, falls eine ganz neue Liga
/// zwischenzeitlich dazukam.
///
/// Ist Kickbase:OnlyLeagueId gesetzt, zählt nur diese eine Liga als "vom Account geliefert" - alle
/// anderen werden über denselben Archivierungs-Pfad stillgelegt (siehe KickbaseOptions).
/// </summary>
public class LeagueSyncService(
    KickbaseApiClient kickbaseApiClient,
    AppDbContext dbContext,
    IOptions<KickbaseOptions> options,
    ILogger<LeagueSyncService> logger)
{
    public async Task<LeagueSyncResult> SyncAsync(CancellationToken ct)
    {
        var me = await kickbaseApiClient.GetMeAsync(ct);
        IReadOnlyList<KickbaseLeagueDto> remoteLeagues = await kickbaseApiClient.GetLeaguesAsync(ct);

        var onlyLeagueId = options.Value.OnlyLeagueId;
        if (!string.IsNullOrWhiteSpace(onlyLeagueId))
        {
            remoteLeagues = remoteLeagues.Where(l => l.Id == onlyLeagueId).ToList();
            if (remoteLeagues.Count == 0)
            {
                logger.LogWarning("Liga-Sync: Kickbase:OnlyLeagueId {LeagueId} gehört nicht (mehr) zum Account - keine aktive Liga.", onlyLeagueId);
            }
        }

        var remoteLeagueIds = remoteLeagues.Select(l => l.Id).ToHashSet();

        var created = 0;
        var updated = 0;

        foreach (var remoteLeague in remoteLeagues)
        {
            var league = await dbContext.Leagues.FindAsync([remoteLeague.Id], ct);
            if (league is null)
            {
                dbContext.Leagues.Add(new League
                {
                    Id = remoteLeague.Id,
                    Name = remoteLeague.Name,
                    CompetitionId = remoteLeague.CompetitionId,
                    OwnUserId = me.Id,
                    IsActive = true,
                });
                created++;
                continue;
            }

            league.Name = remoteLeague.Name;
            league.CompetitionId = remoteLeague.CompetitionId;
            if (!league.IsActive)
            {
                league.IsActive = true; // Account ist der Liga wieder beigetreten.
            }
            else
            {
                updated++;
            }
        }

        var archived = await dbContext.Leagues
            .Where(l => l.IsActive && !remoteLeagueIds.Contains(l.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.IsActive, false), ct);

        await dbContext.SaveChangesAsync(ct);

        return new LeagueSyncResult(created, updated, archived);
    }
}

public record LeagueSyncResult(int Created, int Updated, int Archived);
