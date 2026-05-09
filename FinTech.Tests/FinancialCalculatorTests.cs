using FinTech.API.Utils;

namespace FinTech.Tests;

public class FinancialCalculatorTests
{
    [Fact]
    public void CalculateFixedPayment_ShouldReturnApprox467()
    {
        // TEM = (1 + TEA)^(1/12) - 1 con TEA 24% da ~1.8088%, cuota resultante ~$467.26
        var payment = FinancialCalculator.CalculateFixedPayment(5000m, 0.24m, 12);

        Assert.True(Math.Abs(payment - 467.26m) < 1m);
    }

    [Fact]
    public void GenerateFixedSchedule_ShouldReturnCorrectCount()
    {
        var schedule = FinancialCalculator.GenerateFixedSchedule(5000m, 0.24m, 12, DateTime.Today);

        Assert.Equal(12, schedule.Count);
    }

    [Fact]
    public void GenerateFixedSchedule_LastBalanceShouldBeZero()
    {
        var schedule = FinancialCalculator.GenerateFixedSchedule(5000m, 0.24m, 12, DateTime.Today);

        Assert.Equal(0m, schedule[^1].RemainingBalance);
    }

    [Fact]
    public void GetMonthlyRate_ShouldConvertTEAToTEM()
    {
        var tem = FinancialCalculator.GetMonthlyRate(0.24m);

        Assert.True(tem > 0.017m && tem < 0.019m);
    }
}
