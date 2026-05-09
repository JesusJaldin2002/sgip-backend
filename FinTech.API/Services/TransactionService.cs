using FinTech.API.DTOs.Transactions;
using FinTech.API.Models;
using FinTech.API.Models.Enums;
using FinTech.API.Repositories.Interfaces;
using FinTech.API.Services.Interfaces;

namespace FinTech.API.Services;

public class TransactionService(ITransactionRepository repo) : ITransactionService
{
    private readonly ITransactionRepository _repo = repo;

    public async Task<TransactionResponseDto> CreateTransactionAsync(CreateTransactionDto dto)
    {
        // Idempotencia: si ya existe la key, retornar la original sin crear una nueva
        var existing = await _repo.GetByIdempotencyKeyAsync(dto.IdempotencyKey);
        if (existing != null)
            return MapToResponse(existing);

        var transaction = new Transaction
        {
            IdempotencyKey = dto.IdempotencyKey,
            Type = dto.Type,
            Amount = dto.Amount,
            LoanId = dto.LoanId,
            Description = dto.Description,
            Status = TransactionStatus.Completed
        };

        var created = await _repo.CreateAsync(transaction);
        return MapToResponse(created);
    }

    public async Task<IEnumerable<TransactionResponseDto>> GetTransactionsAsync(string? type, string? status) =>
        (await _repo.GetAllAsync(type, status)).Select(MapToResponse);

    public async Task<TransactionResponseDto?> GetByIdAsync(Guid id)
    {
        var tx = await _repo.GetByIdAsync(id);
        return tx == null ? null : MapToResponse(tx);
    }

    private static TransactionResponseDto MapToResponse(Transaction t) => new()
    {
        Id = t.Id,
        IdempotencyKey = t.IdempotencyKey ?? string.Empty,
        Type = t.Type.ToString() ?? string.Empty,
        Amount = t.Amount,
        Status = t.Status.ToString() ?? string.Empty,
        LoanId = t.LoanId,
        Description = t.Description,
        CreatedAt = t.CreatedAt
    };
}
