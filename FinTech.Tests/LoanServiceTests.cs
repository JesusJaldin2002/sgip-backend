using FinTech.API.DTOs.Loans;
using FinTech.API.DTOs.Transactions;
using FinTech.API.Models;
using FinTech.API.Models.Enums;
using FinTech.API.Repositories.Interfaces;
using FinTech.API.Services;
using FinTech.API.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinTech.Tests;

public class LoanServiceTests
{
    private readonly Mock<ILoanRepository> _mockLoanRepo = new();
    private readonly Mock<ITransactionService> _mockTxService = new();

    private LoanService BuildService() =>
        new(_mockLoanRepo.Object, _mockTxService.Object, NullLogger<LoanService>.Instance);

    private static CreateLoanDto BaseDto(decimal amount = 5000, decimal income = 5000) => new()
    {
        UserId = "user-test",
        Amount = amount,
        Term = 12,
        MonthlyIncome = income,
        LoanType = LoanType.Fixed
    };

    private void SetupCreate(Loan? loan = null)
    {
        _mockLoanRepo.Setup(r => r.CreateAsync(It.IsAny<Loan>()))
                     .ReturnsAsync((Loan l) => l);
        _mockLoanRepo.Setup(r => r.SaveScheduleAsync(It.IsAny<IEnumerable<PaymentSchedule>>()))
                     .Returns(Task.CompletedTask);
        _mockTxService.Setup(t => t.CreateTransactionAsync(It.IsAny<CreateTransactionDto>()))
                      .ReturnsAsync(new TransactionResponseDto());
    }

    [Fact]
    public async Task CreateLoan_WhenUserHas3ActiveLoans_ShouldThrowInvalidOperation()
    {
        var active = Enumerable.Range(0, 3).Select(_ => new Loan { MonthlyPayment = 100m }).ToList();
        _mockLoanRepo.Setup(r => r.GetActiveByUserIdAsync("user-test")).ReturnsAsync(active);

        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildService().CreateLoanAsync(BaseDto()));
    }

    [Fact]
    public async Task CreateLoan_WhenPaymentsExceed40PercentOfIncome_ShouldThrowInvalidOperation()
    {
        // income=500 → cap=200; active already at 200 + new payment pushes over
        var active = new List<Loan> { new Loan { MonthlyPayment = 200m } };
        _mockLoanRepo.Setup(r => r.GetActiveByUserIdAsync("user-test")).ReturnsAsync(active);

        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildService().CreateLoanAsync(BaseDto(income: 500)));
    }

    [Fact]
    public async Task CreateLoan_WhenAmountUnder10000AndFewActiveLoans_ShouldAutoApproveToActive()
    {
        _mockLoanRepo.Setup(r => r.GetActiveByUserIdAsync("user-test")).ReturnsAsync(new List<Loan>());
        SetupCreate();

        var result = await BuildService().CreateLoanAsync(BaseDto(amount: 5000));

        Assert.Equal("Active", result.Status);
    }

    [Fact]
    public async Task CreateLoan_WhenAmountOver10000_ShouldRemainPending()
    {
        _mockLoanRepo.Setup(r => r.GetActiveByUserIdAsync("user-test")).ReturnsAsync(new List<Loan>());
        SetupCreate();

        var result = await BuildService().CreateLoanAsync(BaseDto(amount: 15000));

        Assert.Equal("Pending", result.Status);
    }

    [Fact]
    public async Task CreateLoan_WhenAutoApproved_ShouldCreateDisbursementTransaction()
    {
        _mockLoanRepo.Setup(r => r.GetActiveByUserIdAsync("user-test")).ReturnsAsync(new List<Loan>());
        SetupCreate();

        await BuildService().CreateLoanAsync(BaseDto(amount: 5000));

        _mockTxService.Verify(t => t.CreateTransactionAsync(
            It.Is<CreateTransactionDto>(d => d.Type == TransactionType.Disbursement)), Times.Once);
    }

    [Fact]
    public async Task ApproveLoan_WhenNotFound_ShouldThrowKeyNotFoundException()
    {
        _mockLoanRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Loan?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => BuildService().ApproveLoanAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ApproveLoan_WhenNotPending_ShouldThrowInvalidOperation()
    {
        var loan = new Loan { Id = Guid.NewGuid(), Status = LoanStatus.Active };
        _mockLoanRepo.Setup(r => r.GetByIdAsync(loan.Id)).ReturnsAsync(loan);

        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildService().ApproveLoanAsync(loan.Id));
    }

    [Fact]
    public async Task ApproveLoan_WhenPending_ShouldSetActiveAndCreateDisbursement()
    {
        var loan = new Loan { Id = Guid.NewGuid(), Status = LoanStatus.Pending, UserId = "user-test", Amount = 8000m };
        _mockLoanRepo.Setup(r => r.GetByIdAsync(loan.Id)).ReturnsAsync(loan);
        _mockLoanRepo.Setup(r => r.UpdateAsync(It.IsAny<Loan>())).ReturnsAsync((Loan l) => l);
        _mockTxService.Setup(t => t.CreateTransactionAsync(It.IsAny<CreateTransactionDto>()))
                      .ReturnsAsync(new TransactionResponseDto());

        var result = await BuildService().ApproveLoanAsync(loan.Id);

        Assert.Equal("Active", result.Status);
        _mockTxService.Verify(t => t.CreateTransactionAsync(
            It.Is<CreateTransactionDto>(d => d.Type == TransactionType.Disbursement)), Times.Once);
    }

    [Fact]
    public async Task RejectLoan_WhenNotPending_ShouldThrowInvalidOperation()
    {
        var loan = new Loan { Id = Guid.NewGuid(), Status = LoanStatus.Active };
        _mockLoanRepo.Setup(r => r.GetByIdAsync(loan.Id)).ReturnsAsync(loan);

        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildService().RejectLoanAsync(loan.Id));
    }

    [Fact]
    public async Task RejectLoan_WhenPending_ShouldSetRejected()
    {
        var loan = new Loan { Id = Guid.NewGuid(), Status = LoanStatus.Pending, UserId = "user-test" };
        _mockLoanRepo.Setup(r => r.GetByIdAsync(loan.Id)).ReturnsAsync(loan);
        _mockLoanRepo.Setup(r => r.UpdateAsync(It.IsAny<Loan>())).ReturnsAsync((Loan l) => l);

        var result = await BuildService().RejectLoanAsync(loan.Id);

        Assert.Equal("Rejected", result.Status);
    }
}
