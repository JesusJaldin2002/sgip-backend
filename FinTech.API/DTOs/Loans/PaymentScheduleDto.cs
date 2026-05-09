namespace FinTech.API.DTOs.Loans;

public class PaymentScheduleDto
{
    public int PaymentNumber { get; set; }
    public DateTime DueDate { get; set; }
    public decimal TotalPayment { get; set; }
    public decimal Principal { get; set; }
    public decimal Interest { get; set; }
    public decimal RemainingBalance { get; set; }
    public string Status { get; set; } = "Pending";
}

public class SimulationResponseDto
{
    public decimal Amount { get; set; }
    public int Term { get; set; }
    public decimal InterestRate { get; set; }
    public decimal MonthlyPayment { get; set; }
    public string LoanType { get; set; } = string.Empty;
    public List<PaymentScheduleDto> Schedule { get; set; } = [];
}
