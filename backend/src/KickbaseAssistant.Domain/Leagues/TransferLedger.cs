namespace KickbaseAssistant.Domain.Leagues;

public readonly record struct LedgerTransfer(
    string PlayerId, string PlayerName, TransferType Type, int Price, DateTimeOffset OccurredAt);

public readonly record struct LedgerSquadPlayer(string PlayerId, string PlayerName, int MarketValue);

public readonly record struct AssignedValue(double MarketValue, DateTimeOffset AssignedAt);

public readonly record struct ClosedPosition(
    string PlayerId,
    string PlayerName,
    double AcquisitionValue,
    int SalePrice,
    DateTimeOffset AcquiredAt,
    DateTimeOffset SoldAt,
    bool WasAssigned)
{
    public double Profit => SalePrice - AcquisitionValue;

    public double HeldDays => (SoldAt - AcquiredAt).TotalDays;
}

public readonly record struct OpenPosition(
    string PlayerId,
    string PlayerName,
    double AcquisitionValue,
    int MarketValue,
    DateTimeOffset AcquiredAt,
    bool WasAssigned)
{
    public double Profit => MarketValue - AcquisitionValue;
}

public record TransferLedgerResult(
    IReadOnlyList<ClosedPosition> Closed,
    IReadOnlyList<OpenPosition> Open,
    IReadOnlyList<string> SalesWithoutAcquisitionValue,
    IReadOnlyList<string> SquadPlayersWithoutAcquisitionValue)
{
    public double RealizedProfit => Closed.Sum(position => position.Profit);

    public int ProfitableSalesCount => Closed.Count(position => position.Profit > 0);

    public double? AverageHeldDays => Closed.Count > 0 ? Closed.Average(position => position.HeldDays) : null;

    public double UnrealizedProfit => Open.Sum(position => position.Profit);

    public int AssignedCount =>
        Closed.Count(position => position.WasAssigned) + Open.Count(position => position.WasAssigned);
}

public static class TransferLedger
{
    public static TransferLedgerResult Build(
        IReadOnlyList<LedgerTransfer> transfers,
        IReadOnlyList<LedgerSquadPlayer> squad,
        IReadOnlyDictionary<string, AssignedValue> assignedValues)
    {
        var closed = new List<ClosedPosition>();
        var salesWithoutValue = new List<string>();
        var holdings = new Dictionary<string, (double Value, DateTimeOffset AcquiredAt, bool WasAssigned)>();

        foreach (var playerTransfers in transfers.GroupBy(transfer => transfer.PlayerId))
        {
            (double Value, DateTimeOffset AcquiredAt, bool WasAssigned)? holding = null;
            var isFirstEvent = true;

            foreach (var transfer in playerTransfers.OrderBy(transfer => transfer.OccurredAt))
            {
                if (transfer.Type == TransferType.Buy)
                {
                    holding = (transfer.Price, transfer.OccurredAt, false);
                }
                else
                {
                    if (holding is null && isFirstEvent && assignedValues.TryGetValue(transfer.PlayerId, out var assigned))
                    {
                        holding = (assigned.MarketValue, assigned.AssignedAt, true);
                    }

                    if (holding is { } acquisition)
                    {
                        closed.Add(new ClosedPosition(
                            transfer.PlayerId,
                            transfer.PlayerName,
                            acquisition.Value,
                            transfer.Price,
                            acquisition.AcquiredAt,
                            transfer.OccurredAt,
                            acquisition.WasAssigned));
                    }
                    else
                    {
                        salesWithoutValue.Add(transfer.PlayerName);
                    }

                    holding = null;
                }

                isFirstEvent = false;
            }

            if (holding is { } openHolding)
            {
                holdings[playerTransfers.Key] = openHolding;
            }
        }

        var playersWithTransfers = transfers.Select(transfer => transfer.PlayerId).ToHashSet();
        var open = new List<OpenPosition>();
        var squadWithoutValue = new List<string>();

        foreach (var squadPlayer in squad)
        {
            if (holdings.TryGetValue(squadPlayer.PlayerId, out var holding))
            {
                open.Add(new OpenPosition(
                    squadPlayer.PlayerId, squadPlayer.PlayerName, holding.Value, squadPlayer.MarketValue, holding.AcquiredAt, holding.WasAssigned));
            }
            else if (!playersWithTransfers.Contains(squadPlayer.PlayerId)
                && assignedValues.TryGetValue(squadPlayer.PlayerId, out var assigned))
            {
                open.Add(new OpenPosition(
                    squadPlayer.PlayerId, squadPlayer.PlayerName, assigned.MarketValue, squadPlayer.MarketValue, assigned.AssignedAt, true));
            }
            else
            {
                squadWithoutValue.Add(squadPlayer.PlayerName);
            }
        }

        return new TransferLedgerResult(closed, open, salesWithoutValue, squadWithoutValue);
    }

    public static IReadOnlyList<string> FindAssignedPlayerIds(
        IReadOnlyList<LedgerTransfer> transfers, IReadOnlyCollection<string> squadPlayerIds)
    {
        var soldWithoutBuy = transfers
            .GroupBy(transfer => transfer.PlayerId)
            .Where(playerTransfers => playerTransfers.OrderBy(transfer => transfer.OccurredAt).First().Type == TransferType.Sell)
            .Select(playerTransfers => playerTransfers.Key);

        var playersWithTransfers = transfers.Select(transfer => transfer.PlayerId).ToHashSet();
        var squadWithoutTransfers = squadPlayerIds.Where(playerId => !playersWithTransfers.Contains(playerId));

        return [.. soldWithoutBuy.Concat(squadWithoutTransfers).Distinct()];
    }
}
