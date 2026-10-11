using KickbaseAssistant.Domain.Players;
using Xunit;

namespace KickbaseAssistant.Domain.Tests.Players;

public class FairValueCalculatorTests
{
    [Fact]
    public void FitLinearRegression_PerfectLine_RecoversExactSlopeAndIntercept()
    {
        // y = 2x + 1
        var points = new (double X, double Y)[] { (0, 1), (1, 3), (2, 5), (3, 7) };

        var (slope, intercept) = FairValueCalculator.FitLinearRegression(points);

        Assert.Equal(2, slope, precision: 6);
        Assert.Equal(1, intercept, precision: 6);
    }

    [Fact]
    public void Predict_UsesSlopeAndIntercept()
    {
        var value = FairValueCalculator.Predict(slope: 2, intercept: 1, x: 5);

        Assert.Equal(11, value);
    }

    [Fact]
    public void FitLinearRegression_IdenticalXValues_FallsBackToAverage()
    {
        var points = new (double X, double Y)[] { (5, 10), (5, 20), (5, 30) };

        var (slope, intercept) = FairValueCalculator.FitLinearRegression(points);

        Assert.Equal(0, slope);
        Assert.Equal(20, intercept);
    }

    [Fact]
    public void FitLinearRegression_SinglePoint_InterceptIsThatPoint()
    {
        var (slope, intercept) = FairValueCalculator.FitLinearRegression([(42, 99)]);

        Assert.Equal(0, slope);
        Assert.Equal(99, intercept);
    }

    [Fact]
    public void FitLinearRegression_NoPoints_ReturnsZero()
    {
        var (slope, intercept) = FairValueCalculator.FitLinearRegression([]);

        Assert.Equal(0, slope);
        Assert.Equal(0, intercept);
    }
}
