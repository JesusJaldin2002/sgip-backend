using FinTech.API.DTOs.Loans;
using FinTech.API.DTOs.Transactions;
using FinTech.API.Models;
using FinTech.API.Models.Enums;
using FinTech.API.Repositories.Interfaces;
using FinTech.API.Services.Interfaces;
using FinTech.API.Services.Strategies;

namespace FinTech.API.Services;

public class LoanService(ILoanRepository loanRepo, ITransactionService txService, ILogger<LoanService> logger) : ILoanService
{
    private readonly ILoanRepository _loanRepo = loanRepo;
    private readonly ITransactionService _txService = txService;
    private readonly ILogger<LoanService> _logger = logger;
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
        _logger.LogDebug("Calculando simulacion con estrategia {Strategy}, TEA={TEA}", strategy.GetType().Name, tea);
        var monthlyPayment = strategy.CalculateMonthlyPayment(dto.Amount, tea, dto.Term);
        var startDate = dto.StartDate.HasValue
            ? DateTime.SpecifyKind(dto.StartDate.Value, DateTimeKind.Utc)
            : DateTime.UtcNow;
        var schedule = strategy.GenerateSchedule(dto.Amount, tea, dto.Term, startDate);

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
        _logger.LogInformation("Usuario {UserId} tiene {Count} prestamos activos", dto.UserId, activeLoans.Count);

        if (activeLoans.Count >= 3)
        {
            _logger.LogWarning("Usuario {UserId} supero el limite de 3 prestamos activos", dto.UserId);
            throw new InvalidOperationException("El cliente no puede tener mas de 3 prestamos activos.");
        }

        var strategy = GetStrategy(dto.LoanType);
        var monthlyPayment = strategy.CalculateMonthlyPayment(dto.Amount, tea, dto.Term);

        var totalMonthly = activeLoans.Sum(l => l.MonthlyPayment) + monthlyPayment;
        _logger.LogInformation("Suma de cuotas mensuales: {Total} (limite 40% de {Income} = {Limit})",
            Math.Round(totalMonthly, 2), dto.MonthlyIncome, Math.Round(dto.MonthlyIncome * 0.40m, 2));

        if (totalMonthly > dto.MonthlyIncome * 0.40m)
        {
            _logger.LogWarning("Usuario {UserId} supero el 40% de capacidad de pago", dto.UserId);
            throw new InvalidOperationException("La suma de cuotas supera el 40% de los ingresos mensuales.");
        }

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

        if (dto.Amount < 10000 && activeLoans.Count < 2)
        {
            loan.Status = LoanStatus.Active;
            _logger.LogInformation("Aprobacion automatica: monto={Amount} < $10000 y prestamos activos={Count} < 2", dto.Amount, activeLoans.Count);
        }
        else
        {
            _logger.LogInformation("Prestamo queda en estado Pending (requiere aprobacion manual)");
        }

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
        _logger.LogDebug("Cronograma de {Term} cuotas guardado para prestamo {LoanId}", dto.Term, created.Id);

        if (created.Status == LoanStatus.Active)
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
        {
            _logger.LogWarning("Intento de aprobar prestamo {LoanId} en estado {Status}", id, loan.Status);
            throw new InvalidOperationException("Solo se pueden aprobar prestamos en estado Pending.");
        }

        loan.Status = LoanStatus.Active;
        var updated = await _loanRepo.UpdateAsync(loan);
        _logger.LogInformation("Prestamo {LoanId} aprobado manualmente", id);
        await CreateDisbursementAsync(updated);
        return MapToResponse(updated);
    }

    public async Task<LoanResponseDto> RejectLoanAsync(Guid id)
    {
        var loan = await _loanRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Prestamo no encontrado.");

        if (loan.Status != LoanStatus.Pending)
        {
            _logger.LogWarning("Intento de rechazar prestamo {LoanId} en estado {Status}", id, loan.Status);
            throw new InvalidOperationException("Solo se pueden rechazar prestamos en estado Pending.");
        }

        loan.Status = LoanStatus.Rejected;
        var updated = await _loanRepo.UpdateAsync(loan);
        _logger.LogInformation("Prestamo {LoanId} rechazado", id);
        return MapToResponse(updated);
    }

    private async Task CreateDisbursementAsync(Loan loan)
    {
        _logger.LogInformation("Creando transaccion de desembolso para prestamo {LoanId} por ${Amount}", loan.Id, loan.Amount);
        await _txService.CreateTransactionAsync(new CreateTransactionDto
        {
            IdempotencyKey = $"disbursement-{loan.Id}",
            UserId = loan.UserId,
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
