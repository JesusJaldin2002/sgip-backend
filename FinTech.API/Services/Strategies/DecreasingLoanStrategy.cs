using FinTech.API.Utils;

namespace FinTech.API.Services.Strategies;

public class DecreasingLoanStrategy : ILoanCalculationStrategy
{
    // Retorna la primera cuota (la mas alta) como referencia para el calculo de capacidad de pago
    public decimal CalculateMonthlyPayment(decimal amount, decimal annualRate, int termMonths)
    {
        var tem = FinancialCalculator.GetMonthlyRate(annualRate);
        var principal = Math.Round(amount / termMonths, 2);
        var interest = Math.Round(amount * tem, 2);
        return principal + interest;
    }

    public List<PaymentScheduleEntry> GenerateSchedule(decimal amount, decimal annualRate, int termMonths, DateTime startDate)
        => FinancialCalculator.GenerateDecreasingSchedule(amount, annualRate, termMonths, startDate);
}
