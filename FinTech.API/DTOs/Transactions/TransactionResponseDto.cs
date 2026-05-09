namespace FinTech.API.DTOs.Transactions;

public class TransactionResponseDto
{
    public Guid Id { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? LoanId { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}
