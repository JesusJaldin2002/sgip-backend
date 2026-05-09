using FinTech.API.Utils;

namespace FinTech.API.Services.Strategies;

public class FixedLoanStrategy : ILoanCalculationStrategy
{
    public decimal CalculateMonthlyPayment(decimal amount, decimal annualRate, int termMonths)
        => FinancialCalculator.CalculateFixedPayment(amount, annualRate, termMonths);

    public List<PaymentScheduleEntry> GenerateSchedule(decimal amount, decimal annualRate, int termMonths, DateTime startDate)
        => FinancialCalculator.GenerateFixedSchedule(amount, annualRate, termMonths, startDate);
}
