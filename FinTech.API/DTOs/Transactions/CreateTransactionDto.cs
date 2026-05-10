using FinTech.API.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace FinTech.API.DTOs.Transactions;

public class CreateTransactionDto
{
    [Required]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public TransactionType? Type { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0")]
    public decimal Amount { get; set; }

    public Guid? LoanId { get; set; }

    public string? Description { get; set; }
}
