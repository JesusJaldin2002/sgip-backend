using FinTech.API.Data;
using FinTech.API.Models;
using FinTech.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FinTech.API.Repositories.Implementations;

public class TransactionRepository(ApplicationDbContext ctx) : ITransactionRepository
{
    private readonly ApplicationDbContext _ctx = ctx;

    public async Task<Transaction?> GetByIdAsync(Guid id) =>
        await _ctx.Transactions.FindAsync(id);

    public async Task<Transaction?> GetByIdempotencyKeyAsync(string key) =>
        await _ctx.Transactions.FirstOrDefaultAsync(t => t.IdempotencyKey == key);

    public async Task<IEnumerable<Transaction>> GetAllAsync(string? type = null, string? status = null)
    {
        var query = _ctx.Transactions.AsQueryable();
        if (!string.IsNullOrEmpty(type))
            query = query.Where(t => t.Type.ToString() == type);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status.ToString() == status);
        return await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
    }

    public async Task<Transaction> CreateAsync(Transaction transaction)
    {
        _ctx.Transactions.Add(transaction);
        await _ctx.SaveChangesAsync();
        return transaction;
    }
}
