namespace FinTech.API.Models;

public enum PaymentScheduleStatus { Pending, Paid }

public class PaymentSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LoanId { get; set; }
    public int PaymentNumber { get; set; }
    public DateTime DueDate { get; set; }
    public decimal TotalPayment { get; set; }
    public decimal Principal { get; set; }
    public decimal Interest { get; set; }
    public decimal RemainingBalance { get; set; }
    public PaymentScheduleStatus Status { get; set; } = PaymentScheduleStatus.Pending;

    public Loan Loan { get; set; } = null!;
}
