using KickbaseAssistant.Domain.Leagues;
using Xunit;

namespace KickbaseAssistant.Domain.Tests.Leagues;

public class BalanceGuardCalculatorTests
{
    [Fact]
    public void CreditLimit_PositiveBalance_UsesFullTeamValue()
    {
        var limit = BalanceGuardCalculator.CreditLimit(teamValue: 100_000_000, balance: 5_000_000);

        Assert.Equal(33_000_000, limit, precision: 0);
    }

    [Fact]
    public void CreditLimit_NegativeBalance_MatchesKickbaseWorkedExample()
    {
        // help.kickbase.com/help/wie-weit-darf-ich-ins-minus: Teamwert 100 Mio, Konto -10 Mio
        // -> Limit = 33% von (100 Mio - 10 Mio) = 29,7 Mio.
        var limit = BalanceGuardCalculator.CreditLimit(teamValue: 100_000_000, balance: -10_000_000);

        Assert.Equal(29_700_000, limit, precision: 0);
    }

    [Fact]
    public void GetStatus_PositiveBalance_IsGreenRegardlessOfBuffer()
    {
        Assert.Equal(BalanceStatus.Green, BalanceGuardCalculator.GetStatus(balance: 10, expectedPositiveBuffer: 0));
    }

    [Fact]
    public void GetStatus_NegativeBalance_NoBuffer_IsRed()
    {
        Assert.Equal(BalanceStatus.Red, BalanceGuardCalculator.GetStatus(balance: -5, expectedPositiveBuffer: 0));
    }

    [Fact]
    public void GetStatus_NegativeBalance_DiscountedBufferCoversIt_IsYellow()
    {
        // -5 + 0.7 * 10 = 2 >= 0
        Assert.Equal(BalanceStatus.Yellow, BalanceGuardCalculator.GetStatus(balance: -5, expectedPositiveBuffer: 10));
    }

    [Fact]
    public void GetStatus_NegativeBalance_DiscountedBufferNotEnough_IsRed()
    {
        // -10 + 0.7 * 10 = -3 < 0
        Assert.Equal(BalanceStatus.Red, BalanceGuardCalculator.GetStatus(balance: -10, expectedPositiveBuffer: 10));
    }
}
