using FinTech.API.DTOs.Loans;
using System.ComponentModel.DataAnnotations;

namespace FinTech.Tests;

public class LoanValidationTests
{
    private static IList<ValidationResult> Validate(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, true);
        return results;
    }

    [Theory]
    [InlineData(100)]
    [InlineData(60000)]
    public void CreateLoanDto_InvalidAmount_ShouldFailValidation(decimal amount)
    {
        var dto = new CreateLoanDto { UserId = "user-001", Amount = amount, Term = 12, MonthlyIncome = 3000m };
        var errors = Validate(dto);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateLoanDto.Amount)));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(72)]
    public void CreateLoanDto_InvalidTerm_ShouldFailValidation(int term)
    {
        var dto = new CreateLoanDto { UserId = "user-001", Amount = 5000m, Term = term, MonthlyIncome = 3000m };
        var errors = Validate(dto);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateLoanDto.Term)));
    }
}
