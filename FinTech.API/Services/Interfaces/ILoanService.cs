using FinTech.API.DTOs.Loans;

namespace FinTech.API.Services.Interfaces;

public interface ILoanService
{
    Task<SimulationResponseDto> SimulateAsync(SimulateLoanDto dto);
    Task<LoanResponseDto> CreateLoanAsync(CreateLoanDto dto);
    Task<IEnumerable<LoanResponseDto>> GetLoansAsync(string? userId);
    Task<LoanResponseDto?> GetLoanByIdAsync(Guid id);
    Task<IEnumerable<PaymentScheduleDto>> GetScheduleAsync(Guid loanId);
    Task<LoanResponseDto> ApproveLoanAsync(Guid id);
    Task<LoanResponseDto> RejectLoanAsync(Guid id);
}
