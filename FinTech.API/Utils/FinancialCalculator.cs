namespace FinTech.API.Utils;

public static class FinancialCalculator
{
    // Convierte TEA a TEM: (1 + TEA)^(1/12) - 1
    public static decimal GetMonthlyRate(decimal annualRate)
    {
        return (decimal)(Math.Pow((double)(1 + annualRate), 1.0 / 12) - 1);
    }

    // Sistema Frances: Cuota = Monto * [TEM * (1+TEM)^n] / [(1+TEM)^n - 1]
    public static decimal CalculateFixedPayment(decimal amount, decimal annualRate, int termMonths)
    {
        var tem = GetMonthlyRate(annualRate);
        var factor = (decimal)Math.Pow((double)(1 + tem), termMonths);
        return amount * (tem * factor) / (factor - 1);
    }

    // Cronograma Sistema Frances (cuota fija)
    public static List<PaymentScheduleEntry> GenerateFixedSchedule(
        decimal amount, decimal annualRate, int termMonths, DateTime startDate)
    {
        var tem = GetMonthlyRate(annualRate);
        var monthlyPayment = CalculateFixedPayment(amount, annualRate, termMonths);
        var schedule = new List<PaymentScheduleEntry>();
        var balance = amount;

        for (int i = 1; i <= termMonths; i++)
        {
            var interest = Math.Round(balance * tem, 2);
            var principal = Math.Round(monthlyPayment - interest, 2);

            // Ajuste en ultima cuota para eliminar residuo de decimales
            if (i == termMonths)
                principal = balance;

            balance = Math.Round(balance - principal, 2);

            schedule.Add(new PaymentScheduleEntry
            {
                PaymentNumber = i,
                DueDate = GetDueDate(startDate, i),
                TotalPayment = Math.Round(principal + interest, 2),
                Principal = principal,
                Interest = interest,
                RemainingBalance = balance < 0 ? 0 : balance
            });
        }

        return schedule;
    }

    // Cronograma Sistema Aleman (amortizacion constante) - opcional
    public static List<PaymentScheduleEntry> GenerateDecreasingSchedule(
        decimal amount, decimal annualRate, int termMonths, DateTime startDate)
    {
        var tem = GetMonthlyRate(annualRate);
        var principalPayment = Math.Round(amount / termMonths, 2);
        var schedule = new List<PaymentScheduleEntry>();
        var balance = amount;

        for (int i = 1; i <= termMonths; i++)
        {
            var interest = Math.Round(balance * tem, 2);
            var principal = i == termMonths ? balance : principalPayment;
            balance = Math.Round(balance - principal, 2);

            schedule.Add(new PaymentScheduleEntry
            {
                PaymentNumber = i,
                DueDate = GetDueDate(startDate, i),
                TotalPayment = Math.Round(principal + interest, 2),
                Principal = principal,
                Interest = interest,
                RemainingBalance = balance < 0 ? 0 : balance
            });
        }

        return schedule;
    }

    // Si el dia original usa el ultimo dia del mes
    private static DateTime GetDueDate(DateTime startDate, int monthsToAdd)
    {
        var target = startDate.AddMonths(monthsToAdd);
        var day = Math.Min(startDate.Day, DateTime.DaysInMonth(target.Year, target.Month));
        return new DateTime(target.Year, target.Month, day, 0, 0, 0, startDate.Kind);
    }
}

// DTO aux
public class PaymentScheduleEntry
{
    public int PaymentNumber { get; set; }
    public DateTime DueDate { get; set; }
    public decimal TotalPayment { get; set; }
    public decimal Principal { get; set; }
    public decimal Interest { get; set; }
    public decimal RemainingBalance { get; set; }
}
