using FinTech.API.DTOs.Transactions;
using FinTech.API.Models;
using FinTech.API.Models.Enums;
using FinTech.API.Repositories.Interfaces;
using FinTech.API.Services.Interfaces;

namespace FinTech.API.Services;

public class TransactionService(
    ITransactionRepository repo,
    ILoanRepository loanRepo,
    ILogger<TransactionService> logger) : ITransactionService
{
    private readonly ITransactionRepository _repo = repo;
    private readonly ILoanRepository _loanRepo = loanRepo;
    private readonly ILogger<TransactionService> _logger = logger;

    public async Task<TransactionResponseDto> CreateTransactionAsync(CreateTransactionDto dto)
    {
        var existing = await _repo.GetByIdempotencyKeyAsync(dto.IdempotencyKey);
        if (existing != null)
        {
            _logger.LogInformation("Idempotencia: key={Key} ya existe, retornando transaccion {TransactionId}", dto.IdempotencyKey, existing.Id);
            return MapToResponse(existing);
        }

        var transaction = new Transaction
        {
            IdempotencyKey = dto.IdempotencyKey,
            Type = dto.Type,
            Amount = dto.Amount,
            LoanId = dto.LoanId,
            Description = dto.Description,
            Status = TransactionStatus.Pending
        };

        await _repo.CreateAsync(transaction);
        _logger.LogInformation("Transaccion {TransactionId} creada con estado Pending", transaction.Id);

        var finalStatus = await ProcessAsync(transaction);
        transaction.Status = finalStatus;
        await _repo.UpdateAsync(transaction);

        _logger.LogInformation("Transaccion {TransactionId} resuelta a {Status}", transaction.Id, finalStatus);
        return MapToResponse(transaction);
    }

    private async Task<TransactionStatus> ProcessAsync(Transaction transaction)
    {
        try
        {
            if (transaction.LoanId.HasValue)
            {
                var loan = await _loanRepo.GetByIdAsync(transaction.LoanId.Value);
                if (loan == null)
                {
                    _logger.LogWarning("Transaccion {TransactionId}: LoanId {LoanId} no encontrado", transaction.Id, transaction.LoanId);
                    return TransactionStatus.Failed;
                }
            }

            return TransactionStatus.Completed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando transaccion {TransactionId}", transaction.Id);
            return TransactionStatus.Failed;
        }
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
