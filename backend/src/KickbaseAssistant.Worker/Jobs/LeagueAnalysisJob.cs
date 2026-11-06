using KickbaseAssistant.Domain.Leagues;
using KickbaseAssistant.Infrastructure.Data;
using KickbaseAssistant.Infrastructure.Kickbase;
using KickbaseAssistant.Infrastructure.Kickbase.Dtos;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace KickbaseAssistant.Worker.Jobs;

/// <summary>
/// Phase 8 (Liga-Analyse): logs every league-mate's transfers and estimates their remaining
/// budget and bidding behavior (average overpay vs. market value) - so you know how high you
/// actually need to bid to beat a specific manager, without blindly overpaying yourself.
///
/// Kickbase only exposes the logged-in user's own exact budget (GET .../me/budget), not other
/// managers'. Budget-Schätzung self-calibrates instead: startingBudget is derived once from
/// (my current budget) + (my own net transfer spend), then the same formula (startingBudget -
/// netSpend) is applied to every other manager's own transfer history. This assumes everyone in
/// a league starts with the same budget, which is a standard Kickbase league setting.
///
/// Overpay % needs the market value *at the time* of a purchase, which Kickbase's per-player
/// history only covers for the trailing 92/365 days - so it's only computed for Buy transfers
/// within the last 30 days, looked up once and cached on the LeagueTransfer row from then on.
/// Older transfers are still logged (for the activity history) but without an overpay figure.
///
/// Runs every 30 minutes (see Program.cs) so new transfers - and with them purchase price and
/// profit/loss on Kader/Verkaufsplaner - show up promptly. Existing transfers are only inserted
/// once (keyed by player/type/time), so repeated runs just pick up what's new.
/// </summary>
[DisallowConcurrentExecution]
public class LeagueAnalysisJob(
    KickbaseApiClient kickbaseApiClient,
    AppDbContext dbContext,
    ILogger<LeagueAnalysisJob> logger) : IJob
{
    private const int OverpayLookbackDays = 30;
    private const int MarketValueTimeframeDays = 92;
    private const int AssignedValueTimeframeDays = 365;

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var activeLeagues = await dbContext.Leagues.Where(l => l.IsActive).ToListAsync(ct);
        var managersProcessed = 0;

        foreach (var league in activeLeagues)
        {
            KickbaseRankingDto ranking;
            KickbaseBudgetDto myBudget;
            KickbaseManagerTransfersResponseDto myTransfers;
            try
            {
                ranking = await kickbaseApiClient.GetRankingAsync(league.Id, ct);
                myBudget = await kickbaseApiClient.GetMyBudgetAsync(league.Id, ct);
                myTransfers = await kickbaseApiClient.GetManagerTransfersAsync(league.Id, league.OwnUserId, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Liga-Analyse: Basisdaten für {LeagueName} konnten nicht geladen werden.", league.Name);
                continue;
            }

            var myNetSpend = myTransfers.Transfers.Sum(t => t.IsBuy ? t.Price : -t.Price);
            var startingBudget = myBudget.Budget + myNetSpend;

            foreach (var user in ranking.Users)
            {
                KickbaseManagerTransfersResponseDto transfers;
                try
                {
                    transfers = user.UserId == league.OwnUserId
                        ? myTransfers
                        : await kickbaseApiClient.GetManagerTransfersAsync(league.Id, user.UserId, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Liga-Analyse: Transferverlauf für {ManagerName} konnte nicht geladen werden.", user.Name);
                    continue;
                }

                await ProcessManagerAsync(league, user, transfers, startingBudget, ranking.CurrentMatchday, ct);
                managersProcessed++;
            }

            await RecordAssignedValuesAsync(league, myTransfers, ct);
            await RecordMatchdayResultsAsync(league, ranking.CurrentMatchday, ct);
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Liga-Analyse fertig: {LeagueCount} Ligen, {ManagerCount} Manager verarbeitet.",
            activeLeagues.Count, managersProcessed);
    }

    private async Task ProcessManagerAsync(
        League league,
        KickbaseRankingUserDto rankingUser,
        KickbaseManagerTransfersResponseDto transfers,
        double startingBudget,
        int currentMatchday,
        CancellationToken ct)
    {
        var managerId = rankingUser.UserId;
        var managerName = rankingUser.Name;

        var existingForManager = await dbContext.LeagueTransfers
            .Where(x => x.ManagerId == managerId)
            .ToListAsync(ct);
        var existingLookup = existingForManager.ToDictionary(x => (x.PlayerId, x.Type, x.OccurredAt));

        var overpayPercents = new List<double>();

        foreach (var transfer in transfers.Transfers)
        {
            var type = transfer.IsBuy ? TransferType.Buy : TransferType.Sell;
            existingLookup.TryGetValue((transfer.PlayerId, type, transfer.OccurredAt), out var existing);

            var marketValueAtTransfer = existing?.MarketValueAtTransfer;
            var overpayPercent = existing?.OverpayPercent;

            var isRecentBuy = transfer.IsBuy && transfer.OccurredAt >= DateTimeOffset.UtcNow.AddDays(-OverpayLookbackDays);
            if (isRecentBuy && marketValueAtTransfer is null)
            {
                marketValueAtTransfer = await TryLookupMarketValueAsync(
                    league.CompetitionId, transfer.PlayerId, transfer.OccurredAt, MarketValueTimeframeDays, ct);
                if (marketValueAtTransfer is > 0)
                {
                    overpayPercent = (transfer.Price - marketValueAtTransfer.Value) / marketValueAtTransfer.Value * 100;
                }
            }

            if (existing is null)
            {
                dbContext.LeagueTransfers.Add(new LeagueTransfer
                {
                    LeagueId = league.Id,
                    ManagerId = managerId,
                    ManagerName = managerName,
                    PlayerId = transfer.PlayerId,
                    PlayerName = transfer.PlayerName,
                    Type = type,
                    Price = transfer.Price,
                    OtherManagerName = transfer.OtherManagerName,
                    MarketValueAtTransfer = marketValueAtTransfer,
                    OverpayPercent = overpayPercent,
                    OccurredAt = transfer.OccurredAt,
                });
            }
            else if (overpayPercent.HasValue && existing.OverpayPercent is null)
            {
                existing.MarketValueAtTransfer = marketValueAtTransfer;
                existing.OverpayPercent = overpayPercent;
            }

            if (transfer.IsBuy && overpayPercent.HasValue)
            {
                overpayPercents.Add(overpayPercent.Value);
            }
        }

        var netSpend = transfers.Transfers.Sum(t => t.IsBuy ? t.Price : -t.Price);
        var estimatedBudget = startingBudget - netSpend;

        var stat = await dbContext.LeagueManagerStats
            .FirstOrDefaultAsync(s => s.LeagueId == league.Id && s.ManagerId == managerId, ct);
        if (stat is null)
        {
            stat = new LeagueManagerStat { LeagueId = league.Id, ManagerId = managerId, ManagerName = managerName };
            dbContext.LeagueManagerStats.Add(stat);
        }

        stat.ManagerName = managerName;
        stat.EstimatedBudget = estimatedBudget;
        if (overpayPercents.Count > 0)
        {
            stat.AverageOverpayPercent = overpayPercents.Average();
        }
        stat.TransferCount = transfers.Transfers.Count;
        stat.SeasonPoints = rankingUser.SeasonPoints;
        stat.MatchdayPoints = rankingUser.MatchdayPoints;
        stat.SeasonRank = rankingUser.SeasonRank;
        stat.MatchdayRank = rankingUser.MatchdayRank;
        stat.CurrentMatchday = currentMatchday;
        stat.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private async Task RecordAssignedValuesAsync(
        League league, KickbaseManagerTransfersResponseDto myTransfers, CancellationToken ct)
    {
        if (myTransfers.Transfers.Count == 0)
        {
            return;
        }

        var ledgerTransfers = myTransfers.Transfers
            .Select(transfer => new LedgerTransfer(
                transfer.PlayerId,
                transfer.PlayerName,
                transfer.IsBuy ? TransferType.Buy : TransferType.Sell,
                transfer.Price,
                transfer.OccurredAt))
            .ToList();

        var squadPlayerIds = await dbContext.SquadMemberships
            .Where(membership => membership.LeagueId == league.Id && membership.ManagerId == league.OwnUserId)
            .Select(membership => membership.PlayerId)
            .ToListAsync(ct);

        var knownPlayerIds = await dbContext.AssignedPlayerValues
            .Where(assigned => assigned.LeagueId == league.Id && assigned.ManagerId == league.OwnUserId)
            .Select(assigned => assigned.PlayerId)
            .ToListAsync(ct);

        var missingPlayerIds = TransferLedger.FindAssignedPlayerIds(ledgerTransfers, squadPlayerIds).Except(knownPlayerIds);
        var assignedAt = myTransfers.Transfers.Min(transfer => transfer.OccurredAt);

        foreach (var playerId in missingPlayerIds)
        {
            var marketValue = await TryLookupMarketValueAsync(
                league.CompetitionId, playerId, assignedAt, AssignedValueTimeframeDays, ct);
            if (marketValue is not > 0)
            {
                continue;
            }

            dbContext.AssignedPlayerValues.Add(new AssignedPlayerValue
            {
                LeagueId = league.Id,
                ManagerId = league.OwnUserId,
                PlayerId = playerId,
                AssignedAt = assignedAt,
                MarketValue = marketValue.Value,
            });
        }
    }

    private async Task RecordMatchdayResultsAsync(League league, int currentMatchday, CancellationToken ct)
    {
        if (currentMatchday <= 0)
        {
            return;
        }

        var existing = await dbContext.ManagerMatchdayResults
            .Where(result => result.LeagueId == league.Id)
            .ToListAsync(ct);
        var knownMatchdays = existing.Select(result => result.MatchdayNumber).ToHashSet();
        var existingByKey = existing.ToDictionary(result => (result.ManagerId, result.MatchdayNumber));

        var matchdaysToLoad = Enumerable.Range(1, currentMatchday)
            .Where(matchday => matchday >= currentMatchday - 1 || !knownMatchdays.Contains(matchday));

        foreach (var matchday in matchdaysToLoad)
        {
            KickbaseManagerTeamCenterDto teamCenter;
            try
            {
                teamCenter = await kickbaseApiClient.GetManagerTeamCenterAsync(league.Id, league.OwnUserId, matchday, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Liga-Analyse: Spieltag {Matchday} für {LeagueName} konnte nicht geladen werden.", matchday, league.Name);
                continue;
            }

            foreach (var user in teamCenter.Users)
            {
                if (!existingByKey.TryGetValue((user.Id, matchday), out var result))
                {
                    result = new ManagerMatchdayResult
                    {
                        LeagueId = league.Id,
                        ManagerId = user.Id,
                        ManagerName = user.Name,
                        MatchdayNumber = matchday,
                    };
                    dbContext.ManagerMatchdayResults.Add(result);
                    existingByKey[(user.Id, matchday)] = result;
                }

                result.ManagerName = user.Name;
                result.Points = user.MatchdayPoints;
                result.PointsCounted = user.PointsCounted ?? true;
                result.MatchdayRank = user.MatchdayRank;
                result.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
    }

    private async Task<double?> TryLookupMarketValueAsync(
        string competitionId, string playerId, DateTimeOffset occurredAt, int timeframeDays, CancellationToken ct)
    {
        try
        {
            var history = await kickbaseApiClient.GetMarketValueHistoryAsync(
                competitionId, playerId, timeframeDays, ct);
            if (history.Count == 0)
            {
                return null;
            }

            var targetDayIndex = (occurredAt.UtcDateTime - DateTime.UnixEpoch).TotalDays;
            return history.OrderBy(point => Math.Abs(point.DayIndex - targetDayIndex)).First().MarketValue;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Marktwert-Lookup für Spieler {PlayerId} fehlgeschlagen.", playerId);
            return null;
        }
    }
}
