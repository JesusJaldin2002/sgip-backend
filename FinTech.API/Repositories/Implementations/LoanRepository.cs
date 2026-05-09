using FinTech.API.Data;
using FinTech.API.Models;
using FinTech.API.Models.Enums;
using FinTech.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FinTech.API.Repositories.Implementations;

public class LoanRepository(ApplicationDbContext ctx, ILogger<LoanRepository> logger) : ILoanRepository
{
    private readonly ApplicationDbContext _ctx = ctx;
    private readonly ILogger<LoanRepository> _logger = logger;

    public async Task<Loan?> GetByIdAsync(Guid id)
    {
        _logger.LogDebug("Consultando prestamo {LoanId}", id);
        return await _ctx.Loans
            .Include(l => l.PaymentSchedules)
            .FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task<IEnumerable<Loan>> GetAllAsync(string? userId = null)
    {
        _logger.LogDebug("Listando prestamos (userId={UserId})", userId ?? "todos");
        var query = _ctx.Loans.AsQueryable();
        if (!string.IsNullOrEmpty(userId))
            query = query.Where(l => l.UserId == userId);
        return await query.OrderByDescending(l => l.CreatedAt).ToListAsync();
    }

    public async Task<IEnumerable<Loan>> GetActiveByUserIdAsync(string userId)
    {
        _logger.LogDebug("Consultando prestamos activos de usuario {UserId}", userId);
        return await _ctx.Loans
            .Where(l => l.UserId == userId && l.Status != LoanStatus.Rejected)
            .ToListAsync();
    }

    public async Task<Loan> CreateAsync(Loan loan)
    {
        _logger.LogDebug("Persistiendo prestamo para usuario {UserId}", loan.UserId);
        _ctx.Loans.Add(loan);
        await _ctx.SaveChangesAsync();
        return loan;
    }

    public async Task<Loan> UpdateAsync(Loan loan)
    {
        _logger.LogDebug("Actualizando prestamo {LoanId}", loan.Id);
        loan.UpdatedAt = DateTime.UtcNow;
        _ctx.Loans.Update(loan);
        await _ctx.SaveChangesAsync();
        return loan;
    }

    public async Task<IEnumerable<PaymentSchedule>> GetScheduleByLoanIdAsync(Guid loanId)
    {
        _logger.LogDebug("Consultando cronograma del prestamo {LoanId}", loanId);
        return await _ctx.PaymentSchedules
            .Where(p => p.LoanId == loanId)
            .OrderBy(p => p.PaymentNumber)
            .ToListAsync();
    }

    public async Task SaveScheduleAsync(IEnumerable<PaymentSchedule> schedules)
    {
        _logger.LogDebug("Guardando cronograma de pagos");
        _ctx.PaymentSchedules.AddRange(schedules);
        await _ctx.SaveChangesAsync();
    }
}
