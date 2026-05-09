using FinTech.API.Utils;

namespace FinTech.API.Services.Strategies;

public interface ILoanCalculationStrategy
{
    decimal CalculateMonthlyPayment(decimal amount, decimal annualRate, int termMonths);
    List<PaymentScheduleEntry> GenerateSchedule(decimal amount, decimal annualRate, int termMonths, DateTime startDate);
}
