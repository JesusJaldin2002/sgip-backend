using FinTech.API.DTOs.Transactions;
using FinTech.API.Models;
using FinTech.API.Models.Enums;
using FinTech.API.Repositories.Interfaces;
using FinTech.API.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinTech.Tests;

public class TransactionStatusTests
{
    private readonly Mock<ITransactionRepository> _mockTxRepo = new();
    private readonly Mock<ILoanRepository> _mockLoanRepo = new();

    private TransactionService BuildService() =>
        new(_mockTxRepo.Object, _mockLoanRepo.Object, NullLogger<TransactionService>.Instance);

    private void SetupNewTransaction()
    {
        _mockTxRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>()))
                   .ReturnsAsync((Transaction?)null);
        _mockTxRepo.Setup(r => r.CreateAsync(It.IsAny<Transaction>()))
                   .ReturnsAsync((Transaction t) => t);
        _mockTxRepo.Setup(r => r.UpdateAsync(It.IsAny<Transaction>()))
                   .ReturnsAsync((Transaction t) => t);
    }

    [Fact]
    public async Task CreateTransaction_WithoutLoanId_ShouldTransitionToCompleted()
    {
        SetupNewTransaction();

        var service = BuildService();
        var dto = new CreateTransactionDto
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            Type = TransactionType.Payment,
            Amount = 300m
        };

        var result = await service.CreateTransactionAsync(dto);

        Assert.Equal("Completed", result.Status);
        _mockTxRepo.Verify(r => r.UpdateAsync(It.Is<Transaction>(t => t.Status == TransactionStatus.Completed)), Times.Once);
    }

    [Fact]
    public async Task CreateTransaction_WithValidLoanId_ShouldTransitionToCompleted()
    {
        SetupNewTransaction();
        var loanId = Guid.NewGuid();
        _mockLoanRepo.Setup(r => r.GetByIdAsync(loanId))
                     .ReturnsAsync(new Loan { Id = loanId, UserId = "user-001", Amount = 5000m });

        var service = BuildService();
        var dto = new CreateTransactionDto
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            Type = TransactionType.Payment,
            Amount = 467m,
            LoanId = loanId
        };

        var result = await service.CreateTransactionAsync(dto);

        Assert.Equal("Completed", result.Status);
    }

    [Fact]
    public async Task CreateTransaction_WithNonExistentLoanId_ShouldTransitionToFailed()
    {
        SetupNewTransaction();
        var invalidLoanId = Guid.NewGuid();
        _mockLoanRepo.Setup(r => r.GetByIdAsync(invalidLoanId))
                     .ReturnsAsync((Loan?)null);

        var service = BuildService();
        var dto = new CreateTransactionDto
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            Type = TransactionType.Payment,
            Amount = 467m,
            LoanId = invalidLoanId
        };

        var result = await service.CreateTransactionAsync(dto);

        Assert.Equal("Failed", result.Status);
        _mockTxRepo.Verify(r => r.UpdateAsync(It.Is<Transaction>(t => t.Status == TransactionStatus.Failed)), Times.Once);
    }
}
