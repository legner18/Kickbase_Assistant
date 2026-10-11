using KickbaseAssistant.Domain.Players;
using Xunit;

namespace KickbaseAssistant.Domain.Tests.Players;

public class MarketChangeDetectorTests
{
    private static MarketListingSnapshot Listing(string playerId, int price, int offerCount) =>
        new(playerId, $"Player {playerId}", price, offerCount, DateTimeOffset.UtcNow.AddHours(6));

    [Fact]
    public void Detect_NewListing_ReportsNew()
    {
        var previous = new Dictionary<string, MarketListingSnapshot>();
        var current = new Dictionary<string, MarketListingSnapshot> { ["1"] = Listing("1", 1_000_000, 0) };

        var changes = MarketChangeDetector.Detect(previous, current);

        var change = Assert.Single(changes);
        Assert.Equal(MarketChangeType.New, change.Type);
        Assert.Equal("1", change.PlayerId);
    }

    [Fact]
    public void Detect_RemovedListing_ReportsRemoved()
    {
        var previous = new Dictionary<string, MarketListingSnapshot> { ["1"] = Listing("1", 1_000_000, 0) };
        var current = new Dictionary<string, MarketListingSnapshot>();

        var changes = MarketChangeDetector.Detect(previous, current);

        var change = Assert.Single(changes);
        Assert.Equal(MarketChangeType.Removed, change.Type);
    }

    [Fact]
    public void Detect_PriceChange_ReportsPreviousAndCurrentValue()
    {
        var previous = new Dictionary<string, MarketListingSnapshot> { ["1"] = Listing("1", 1_000_000, 0) };
        var current = new Dictionary<string, MarketListingSnapshot> { ["1"] = Listing("1", 1_200_000, 0) };

        var changes = MarketChangeDetector.Detect(previous, current);

        var change = Assert.Single(changes);
        Assert.Equal(MarketChangeType.PriceChanged, change.Type);
        Assert.Equal(1_000_000, change.PreviousValue);
        Assert.Equal(1_200_000, change.CurrentValue);
    }

    [Fact]
    public void Detect_OfferCountChange_ReportsIt()
    {
        var previous = new Dictionary<string, MarketListingSnapshot> { ["1"] = Listing("1", 1_000_000, 1) };
        var current = new Dictionary<string, MarketListingSnapshot> { ["1"] = Listing("1", 1_000_000, 2) };

        var changes = MarketChangeDetector.Detect(previous, current);

        var change = Assert.Single(changes);
        Assert.Equal(MarketChangeType.OfferCountChanged, change.Type);
        Assert.Equal(1, change.PreviousValue);
        Assert.Equal(2, change.CurrentValue);
    }

    [Fact]
    public void Detect_Unchanged_ReportsNothing()
    {
        var previous = new Dictionary<string, MarketListingSnapshot> { ["1"] = Listing("1", 1_000_000, 1) };
        var current = new Dictionary<string, MarketListingSnapshot> { ["1"] = Listing("1", 1_000_000, 1) };

        var changes = MarketChangeDetector.Detect(previous, current);

        Assert.Empty(changes);
    }

    [Fact]
    public void Detect_PriceAndOfferCountBothChange_ReportsBoth()
    {
        var previous = new Dictionary<string, MarketListingSnapshot> { ["1"] = Listing("1", 1_000_000, 1) };
        var current = new Dictionary<string, MarketListingSnapshot> { ["1"] = Listing("1", 1_100_000, 2) };

        var changes = MarketChangeDetector.Detect(previous, current);

        Assert.Equal(2, changes.Count);
        Assert.Contains(changes, c => c.Type == MarketChangeType.PriceChanged);
        Assert.Contains(changes, c => c.Type == MarketChangeType.OfferCountChanged);
    }
}
