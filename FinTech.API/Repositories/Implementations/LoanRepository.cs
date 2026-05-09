using FinTech.API.Data;
using FinTech.API.Models;
using FinTech.API.Models.Enums;
using FinTech.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FinTech.API.Repositories.Implementations;

public class LoanRepository(ApplicationDbContext ctx) : ILoanRepository
{
    private readonly ApplicationDbContext _ctx = ctx;

    public async Task<Loan?> GetByIdAsync(Guid id) =>
        await _ctx.Loans
            .Include(l => l.PaymentSchedules)
            .FirstOrDefaultAsync(l => l.Id == id);

    public async Task<IEnumerable<Loan>> GetAllAsync(string? userId = null)
    {
        var query = _ctx.Loans.AsQueryable();
        if (!string.IsNullOrEmpty(userId))
            query = query.Where(l => l.UserId == userId);
        return await query.OrderByDescending(l => l.CreatedAt).ToListAsync();
    }

    public async Task<IEnumerable<Loan>> GetActiveByUserIdAsync(string userId) =>
        await _ctx.Loans
            .Where(l => l.UserId == userId && l.Status == LoanStatus.Active)
            .ToListAsync();

    public async Task<Loan> CreateAsync(Loan loan)
    {
        _ctx.Loans.Add(loan);
        await _ctx.SaveChangesAsync();
        return loan;
    }

    public async Task<Loan> UpdateAsync(Loan loan)
    {
        loan.UpdatedAt = DateTime.UtcNow;
        _ctx.Loans.Update(loan);
        await _ctx.SaveChangesAsync();
        return loan;
    }

    public async Task<IEnumerable<PaymentSchedule>> GetScheduleByLoanIdAsync(Guid loanId) =>
        await _ctx.PaymentSchedules
            .Where(p => p.LoanId == loanId)
            .OrderBy(p => p.PaymentNumber)
            .ToListAsync();

    public async Task SaveScheduleAsync(IEnumerable<PaymentSchedule> schedules)
    {
        _ctx.PaymentSchedules.AddRange(schedules);
        await _ctx.SaveChangesAsync();
    }
}
