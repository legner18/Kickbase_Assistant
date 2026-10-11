using KickbaseAssistant.Domain.Players;
using Xunit;

namespace KickbaseAssistant.Domain.Tests.Players;

public class MarketValuePredictorTests
{
    [Fact]
    public void PredictNextChangePercent_NoHistory_ReturnsZero()
    {
        Assert.Equal(0, MarketValuePredictor.PredictNextChangePercent([]));
    }

    [Fact]
    public void PredictNextChangePercent_ConstantTrend_ReturnsSameValue()
    {
        Assert.Equal(2.0, MarketValuePredictor.PredictNextChangePercent([2.0, 2.0, 2.0]), precision: 6);
    }

    [Fact]
    public void PredictNextChangePercent_WeightsRecentDaysMoreHeavily()
    {
        // Älteste zuerst: [0%, 0%, 6%] - der jüngste, höher gewichtete Tag zieht die Prognose
        // deutlich näher an 6% als ein ungewichteter Durchschnitt (2%) es täte.
        var result = MarketValuePredictor.PredictNextChangePercent([0.0, 0.0, 6.0]);

        Assert.True(result > 2.0);
    }

    [Fact]
    public void PredictNextValue_AppliesPercentToCurrentValue()
    {
        var predicted = MarketValuePredictor.PredictNextValue(1_000_000, 5.0);

        Assert.Equal(1_050_000, predicted, precision: 2);
    }

    [Fact]
    public void PredictNextValue_NegativeChange_DecreasesValue()
    {
        var predicted = MarketValuePredictor.PredictNextValue(1_000_000, -10.0);

        Assert.Equal(900_000, predicted, precision: 2);
    }
}
