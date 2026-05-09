using FinTech.API.Models;

namespace FinTech.API.Repositories.Interfaces;

public interface ILoanRepository
{
    Task<Loan?> GetByIdAsync(Guid id);
    Task<IEnumerable<Loan>> GetAllAsync(string? userId = null);
    Task<IEnumerable<Loan>> GetActiveByUserIdAsync(string userId);
    Task<Loan> CreateAsync(Loan loan);
    Task<Loan> UpdateAsync(Loan loan);
    Task<IEnumerable<PaymentSchedule>> GetScheduleByLoanIdAsync(Guid loanId);
    Task SaveScheduleAsync(IEnumerable<PaymentSchedule> schedules);
}
