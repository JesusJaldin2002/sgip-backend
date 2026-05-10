using FinTech.API.Models;

namespace FinTech.API.Repositories.Interfaces;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id);
    Task<Transaction?> GetByIdempotencyKeyAsync(string key);
    Task<IEnumerable<Transaction>> GetAllAsync(string? type = null, string? status = null);
    Task<Transaction> CreateAsync(Transaction transaction);
    Task<Transaction> UpdateAsync(Transaction transaction);
}
