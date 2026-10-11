using KickbaseAssistant.Domain.Players;
using Xunit;

namespace KickbaseAssistant.Domain.Tests.Players;

public class MarketValueTrendAnalyzerTests
{
    [Fact]
    public void Analyze_FewerThanThreeDays_ReturnsUnknown()
    {
        Assert.Equal(MarketValueTrendStatus.Unknown, MarketValueTrendAnalyzer.Analyze([1.0, 2.0]));
    }

    [Fact]
    public void Analyze_SteadyStrongGains_ReturnsRising()
    {
        Assert.Equal(MarketValueTrendStatus.Rising, MarketValueTrendAnalyzer.Analyze([2.0, 2.5, 3.0]));
    }

    [Fact]
    public void Analyze_ThreeDaysDeceleratingButStillPositive_ReturnsPeakApproaching()
    {
        Assert.Equal(MarketValueTrendStatus.PeakApproaching, MarketValueTrendAnalyzer.Analyze([5.0, 3.0, 1.0]));
    }

    [Fact]
    public void Analyze_LatestChangeNegative_ReturnsSellSignal()
    {
        Assert.Equal(MarketValueTrendStatus.SellSignal, MarketValueTrendAnalyzer.Analyze([5.0, 3.0, -0.5]));
    }

    [Fact]
    public void Analyze_LatestChangeBelowThresholdButPositive_ReturnsSellSignal()
    {
        Assert.Equal(MarketValueTrendStatus.SellSignal, MarketValueTrendAnalyzer.Analyze([5.0, 3.0, 0.2]));
    }

    [Fact]
    public void Analyze_NotMonotonicallyDecreasing_ReturnsRising()
    {
        // Tag 2 höher als Tag 1 -> keine durchgehende Abflachung, trotzdem über Schwelle.
        Assert.Equal(MarketValueTrendStatus.Rising, MarketValueTrendAnalyzer.Analyze([1.0, 4.0, 2.0]));
    }
}
