using FinTech.API.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace FinTech.API.DTOs.Loans;

public class SimulateLoanDto
{
    [Required]
    [Range(500, 50000, ErrorMessage = "El monto debe estar entre $500 y $50,000")]
    public decimal Amount { get; set; }

    [Required]
    [Range(6, 60, ErrorMessage = "El plazo debe estar entre 6 y 60 meses")]
    public int Term { get; set; }

    [Required]
    public LoanType LoanType { get; set; } = LoanType.Fixed;

    [Range(0.18, 0.35, ErrorMessage = "La tasa debe estar entre 18% y 35%")]
    public decimal? InterestRate { get; set; }
}
