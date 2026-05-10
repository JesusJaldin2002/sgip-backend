using FinTech.API.Data;
using FinTech.API.Models;
using FinTech.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FinTech.API.Repositories.Implementations;

public class TransactionRepository(ApplicationDbContext ctx, ILogger<TransactionRepository> logger) : ITransactionRepository
{
    private readonly ApplicationDbContext _ctx = ctx;
    private readonly ILogger<TransactionRepository> _logger = logger;

    public async Task<Transaction?> GetByIdAsync(Guid id)
    {
        _logger.LogDebug("Consultando transaccion {TransactionId}", id);
        return await _ctx.Transactions.FindAsync(id);
    }

    public async Task<Transaction?> GetByIdempotencyKeyAsync(string key)
    {
        _logger.LogDebug("Verificando idempotency key={Key}", key);
        return await _ctx.Transactions.FirstOrDefaultAsync(t => t.IdempotencyKey == key);
    }

    public async Task<IEnumerable<Transaction>> GetAllAsync(string? type = null, string? status = null)
    {
        _logger.LogDebug("Listando transacciones (tipo={Type}, estado={Status})", type ?? "todos", status ?? "todos");
        var query = _ctx.Transactions.AsQueryable();
        if (!string.IsNullOrEmpty(type))
            query = query.Where(t => t.Type.ToString() == type);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status.ToString() == status);
        return await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
    }

    public async Task<Transaction> CreateAsync(Transaction transaction)
    {
        _logger.LogDebug("Persistiendo transaccion con key={Key}", transaction.IdempotencyKey);
        _ctx.Transactions.Add(transaction);
        await _ctx.SaveChangesAsync();
        return transaction;
    }

    public async Task<Transaction> UpdateAsync(Transaction transaction)
    {
        _logger.LogDebug("Actualizando transaccion {TransactionId} a estado {Status}", transaction.Id, transaction.Status);
        _ctx.Transactions.Update(transaction);
        await _ctx.SaveChangesAsync();
        return transaction;
    }
}
