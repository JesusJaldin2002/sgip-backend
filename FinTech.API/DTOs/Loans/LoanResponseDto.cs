namespace FinTech.API.DTOs.Loans;

public class LoanResponseDto
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Term { get; set; }
    public decimal InterestRate { get; set; }
    public string LoanType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal MonthlyPayment { get; set; }
    public decimal MonthlyIncome { get; set; }
    public DateTime CreatedAt { get; set; }
}
