using FinTech.API.DTOs.Transactions;
using FinTech.API.Models;
using FinTech.API.Models.Enums;
using FinTech.API.Repositories.Interfaces;
using FinTech.API.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinTech.Tests;

public class TransactionIdempotencyTests
{
    [Fact]
    public async Task CreateTransaction_WithExistingKey_ShouldReturnOriginalAndNeverCallCreate()
    {
        var existingTx = new Transaction
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = "test-key-123",
            Type = TransactionType.Payment,
            Amount = 500m,
            Status = TransactionStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };

        var mockRepo = new Mock<ITransactionRepository>();
        mockRepo.Setup(r => r.GetByIdempotencyKeyAsync("test-key-123"))
                .ReturnsAsync(existingTx);

        var mockLoanRepo = new Mock<ILoanRepository>();

        var service = new TransactionService(mockRepo.Object, mockLoanRepo.Object, NullLogger<TransactionService>.Instance);

        var dto = new CreateTransactionDto
        {
            IdempotencyKey = "test-key-123",
            UserId = "user-001",
            Type = TransactionType.Payment,
            Amount = 999m
        };

        var result = await service.CreateTransactionAsync(dto);

        Assert.Equal(existingTx.Id, result.Id);
        Assert.Equal(500m, result.Amount);
        mockRepo.Verify(r => r.CreateAsync(It.IsAny<Transaction>()), Times.Never);
    }
}
