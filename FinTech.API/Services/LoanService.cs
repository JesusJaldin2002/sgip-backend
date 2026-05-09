using FinTech.API.DTOs.Loans;
using FinTech.API.DTOs.Transactions;
using FinTech.API.Models;
using FinTech.API.Models.Enums;
using FinTech.API.Repositories.Interfaces;
using FinTech.API.Services.Interfaces;
using FinTech.API.Services.Strategies;

namespace FinTech.API.Services;

public class LoanService(ILoanRepository loanRepo, ITransactionService txService) : ILoanService
{
    private readonly ILoanRepository _loanRepo = loanRepo;
    private readonly ITransactionService _txService = txService;
    private const decimal DefaultTEA = 0.24m;

    private static ILoanCalculationStrategy GetStrategy(LoanType loanType) => loanType switch
    {
        LoanType.Decreasing => new DecreasingLoanStrategy(),
        _ => new FixedLoanStrategy()
    };

    public async Task<SimulationResponseDto> SimulateAsync(SimulateLoanDto dto)
    {
        var tea = dto.InterestRate ?? DefaultTEA;
        var strategy = GetStrategy(dto.LoanType);
        var monthlyPayment = strategy.CalculateMonthlyPayment(dto.Amount, tea, dto.Term);
        var schedule = strategy.GenerateSchedule(dto.Amount, tea, dto.Term, DateTime.UtcNow);

        return new SimulationResponseDto
        {
            Amount = dto.Amount,
            Term = dto.Term,
            InterestRate = tea,
            MonthlyPayment = Math.Round(monthlyPayment, 2),
            LoanType = dto.LoanType.ToString(),
            Schedule = [.. schedule.Select(s => new PaymentScheduleDto
            {
                PaymentNumber = s.PaymentNumber,
                DueDate = s.DueDate,
                TotalPayment = s.TotalPayment,
                Principal = s.Principal,
                Interest = s.Interest,
                RemainingBalance = s.RemainingBalance
            })]
        };
    }

    public async Task<LoanResponseDto> CreateLoanAsync(CreateLoanDto dto)
    {
        var tea = dto.InterestRate ?? DefaultTEA;

        var activeLoans = (await _loanRepo.GetActiveByUserIdAsync(dto.UserId)).ToList();

        if (activeLoans.Count >= 3)
            throw new InvalidOperationException("El cliente no puede tener mas de 3 prestamos activos.");

        var strategy = GetStrategy(dto.LoanType);
        var monthlyPayment = strategy.CalculateMonthlyPayment(dto.Amount, tea, dto.Term);

        var totalMonthly = activeLoans.Sum(l => l.MonthlyPayment) + monthlyPayment;
        if (totalMonthly > dto.MonthlyIncome * 0.40m)
            throw new InvalidOperationException("La suma de cuotas supera el 40% de los ingresos mensuales.");

        var loan = new Loan
        {
            UserId = dto.UserId,
            Amount = dto.Amount,
            Term = dto.Term,
            InterestRate = tea,
            LoanType = dto.LoanType,
            MonthlyPayment = Math.Round(monthlyPayment, 2),
            MonthlyIncome = dto.MonthlyIncome
        };

        // Aprobacion automatica: monto < $10,000 y menos de 2 prestamos activos
        if (dto.Amount < 10000 && activeLoans.Count < 2)
            loan.Status = LoanStatus.Approved;

        var created = await _loanRepo.CreateAsync(loan);

        var scheduleEntries = strategy.GenerateSchedule(dto.Amount, tea, dto.Term, DateTime.UtcNow);

        var schedules = scheduleEntries.Select(s => new PaymentSchedule
        {
            LoanId = created.Id,
            PaymentNumber = s.PaymentNumber,
            DueDate = s.DueDate,
            TotalPayment = s.TotalPayment,
            Principal = s.Principal,
            Interest = s.Interest,
            RemainingBalance = s.RemainingBalance
        });

        await _loanRepo.SaveScheduleAsync(schedules);

        if (created.Status == LoanStatus.Approved)
            await CreateDisbursementAsync(created);

        return MapToResponse(created);
    }

    public async Task<IEnumerable<LoanResponseDto>> GetLoansAsync(string? userId) =>
        (await _loanRepo.GetAllAsync(userId)).Select(MapToResponse);

    public async Task<LoanResponseDto?> GetLoanByIdAsync(Guid id)
    {
        var loan = await _loanRepo.GetByIdAsync(id);
        return loan == null ? null : MapToResponse(loan);
    }

    public async Task<IEnumerable<PaymentScheduleDto>> GetScheduleAsync(Guid loanId)
    {
        var schedules = await _loanRepo.GetScheduleByLoanIdAsync(loanId);
        return schedules.Select(s => new PaymentScheduleDto
        {
            PaymentNumber = s.PaymentNumber,
            DueDate = s.DueDate,
            TotalPayment = s.TotalPayment,
            Principal = s.Principal,
            Interest = s.Interest,
            RemainingBalance = s.RemainingBalance,
            Status = s.Status.ToString()
        });
    }

    public async Task<LoanResponseDto> ApproveLoanAsync(Guid id)
    {
        var loan = await _loanRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Prestamo no encontrado.");

        if (loan.Status != LoanStatus.Pending)
            throw new InvalidOperationException("Solo se pueden aprobar prestamos en estado Pending.");

        loan.Status = LoanStatus.Approved;
        var updated = await _loanRepo.UpdateAsync(loan);
        await CreateDisbursementAsync(updated);
        return MapToResponse(updated);
    }

    public async Task<LoanResponseDto> RejectLoanAsync(Guid id)
    {
        var loan = await _loanRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Prestamo no encontrado.");

        if (loan.Status != LoanStatus.Pending)
            throw new InvalidOperationException("Solo se pueden rechazar prestamos en estado Pending.");

        loan.Status = LoanStatus.Rejected;
        var updated = await _loanRepo.UpdateAsync(loan);
        return MapToResponse(updated);
    }

    private async Task CreateDisbursementAsync(Loan loan)
    {
        await _txService.CreateTransactionAsync(new CreateTransactionDto
        {
            IdempotencyKey = $"disbursement-{loan.Id}",
            Type = TransactionType.Disbursement,
            Amount = loan.Amount,
            LoanId = loan.Id,
            Description = $"Desembolso prestamo {loan.Id}"
        });
    }

    private static LoanResponseDto MapToResponse(Loan l) => new()
    {
        Id = l.Id,
        UserId = l.UserId,
        Amount = l.Amount,
        Term = l.Term,
        InterestRate = l.InterestRate,
        LoanType = l.LoanType.ToString(),
        Status = l.Status.ToString(),
        MonthlyPayment = l.MonthlyPayment,
        MonthlyIncome = l.MonthlyIncome,
        CreatedAt = l.CreatedAt
    };
}
