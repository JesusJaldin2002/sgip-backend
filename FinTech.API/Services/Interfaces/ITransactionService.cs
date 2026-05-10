using FinTech.API.DTOs.Transactions;

namespace FinTech.API.Services.Interfaces;

public interface ITransactionService
{
    Task<TransactionResponseDto> CreateTransactionAsync(CreateTransactionDto dto);
    Task<IEnumerable<TransactionResponseDto>> GetTransactionsAsync(string? type, string? status, string? userId = null);
    Task<TransactionResponseDto?> GetByIdAsync(Guid id);
}
