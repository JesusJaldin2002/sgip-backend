using FinTech.API.DTOs.Loans;
using FinTech.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FinTech.API.Controllers;

/// <summary>Gestion de prestamos: simulacion, solicitud, aprobacion y cronograma de pagos.</summary>
[ApiController]
[Route("api/loans")]
[Produces("application/json")]
public class LoansController(ILoanService svc, ILogger<LoansController> logger) : ControllerBase
{
    private readonly ILoanService _svc = svc;
    private readonly ILogger<LoansController> _logger = logger;

    /// <summary>Simula un prestamo y retorna el cronograma de pagos sin guardar en base de datos.</summary>
    /// <remarks>
    /// Calcula la cuota mensual y genera el cronograma completo usando el sistema Frances (cuota fija)
    /// o Aleman (cuota decreciente). No persiste ningun dato.
    ///
    /// Si no se proporciona <c>InterestRate</c>, se usa la TEA default de 24% (0.24).
    /// Si no se proporciona <c>StartDate</c>, se usa la fecha actual.
    /// </remarks>
    /// <param name="dto">Parametros de simulacion: monto, plazo, tipo de cuota, tasa opcional y fecha de inicio opcional.</param>
    [HttpPost("simulate")]
    [ProducesResponseType(typeof(SimulationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Simulate([FromBody] SimulateLoanDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        _logger.LogInformation("Simulando prestamo: monto={Amount}, plazo={Term}, tipo={LoanType}", dto.Amount, dto.Term, dto.LoanType);
        var result = await _svc.SimulateAsync(dto);
        return Ok(result);
    }

    /// <summary>Crea una solicitud de prestamo para un usuario.</summary>
    /// <remarks>
    /// Aplica todas las validaciones de negocio antes de persistir:
    ///
    /// - El usuario no puede tener mas de 3 prestamos activos.
    /// - La suma de cuotas mensuales de todos sus prestamos no puede superar el 40% de su ingreso mensual.
    ///
    /// **Auto-aprobacion:** Si el monto es menor a $10,000 y el usuario tiene menos de 2 prestamos activos,
    /// el prestamo se aprueba automaticamente (estado `Active`) y se genera una transaccion de desembolso.
    /// En caso contrario, queda en estado `Pending` esperando aprobacion manual.
    /// </remarks>
    /// <param name="dto">Datos del prestamo: userId, monto, plazo, tipo, tasa opcional e ingreso mensual.</param>
    [HttpPost]
    [ProducesResponseType(typeof(LoanResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateLoanDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        _logger.LogInformation("Solicitud de prestamo recibida para usuario {UserId}: monto={Amount}, plazo={Term}", dto.UserId, dto.Amount, dto.Term);
        try
        {
            var result = await _svc.CreateLoanAsync(dto);
            _logger.LogInformation("Prestamo {LoanId} creado con estado {Status}", result.Id, result.Status);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Solicitud rechazada para usuario {UserId}: {Reason}", dto.UserId, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Lista todos los prestamos, con filtro opcional por usuario.</summary>
    /// <remarks>
    /// Si se omite <c>userId</c>, retorna todos los prestamos del sistema.
    /// Los resultados incluyen: ID, monto, plazo, tasa, tipo, estado, cuota mensual e ingreso mensual.
    /// </remarks>
    /// <param name="userId">ID del usuario para filtrar (ej: "user-001"). Opcional.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LoanResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? userId)
    {
        _logger.LogInformation("Listando prestamos (userId={UserId})", userId ?? "todos");
        var result = await _svc.GetLoansAsync(userId);
        return Ok(result);
    }

    /// <summary>Obtiene un prestamo especifico por su ID.</summary>
    /// <param name="id">GUID del prestamo.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LoanResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        _logger.LogInformation("Consultando prestamo {LoanId}", id);
        var result = await _svc.GetLoanByIdAsync(id);
        if (result == null)
        {
            _logger.LogWarning("Prestamo {LoanId} no encontrado", id);
            return NotFound();
        }
        return Ok(result);
    }

    /// <summary>Retorna el cronograma completo de pagos de un prestamo.</summary>
    /// <remarks>
    /// Cada entrada del cronograma incluye: numero de cuota, fecha de vencimiento, cuota total,
    /// amortizacion de capital, interes del periodo y saldo restante.
    ///
    /// Las fechas siguen la regla "mismo dia del mes". Si el dia cae en 31 y el mes tiene 30 dias,
    /// se usa el dia 30.
    /// </remarks>
    /// <param name="id">GUID del prestamo.</param>
    [HttpGet("{id:guid}/schedule")]
    [ProducesResponseType(typeof(IEnumerable<PaymentScheduleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSchedule(Guid id)
    {
        _logger.LogInformation("Consultando cronograma del prestamo {LoanId}", id);
        var result = await _svc.GetScheduleAsync(id);
        return Ok(result);
    }

    /// <summary>Aprueba un prestamo en estado Pending y genera la transaccion de desembolso.</summary>
    /// <remarks>
    /// Solo los prestamos en estado <c>Pending</c> pueden ser aprobados.
    /// Al aprobar, el estado cambia a <c>Active</c> y se crea automaticamente una transaccion
    /// de tipo <c>Disbursement</c> con el monto total del prestamo.
    ///
    /// Retorna 400 si el prestamo no esta en estado Pending.
    /// </remarks>
    /// <param name="id">GUID del prestamo a aprobar.</param>
    [HttpPatch("{id:guid}/approve")]
    [ProducesResponseType(typeof(LoanResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(Guid id)
    {
        _logger.LogInformation("Solicitud de aprobacion para prestamo {LoanId}", id);
        try
        {
            var result = await _svc.ApproveLoanAsync(id);
            _logger.LogInformation("Prestamo {LoanId} aprobado", id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Prestamo {LoanId} no encontrado para aprobar", id);
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "No se pudo aprobar prestamo {LoanId}: {Reason}", id, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Rechaza un prestamo en estado Pending.</summary>
    /// <remarks>
    /// Solo los prestamos en estado <c>Pending</c> pueden ser rechazados.
    /// Al rechazar, el estado cambia a <c>Rejected</c> y no se genera ninguna transaccion.
    ///
    /// Retorna 400 si el prestamo no esta en estado Pending.
    /// </remarks>
    /// <param name="id">GUID del prestamo a rechazar.</param>
    [HttpPatch("{id:guid}/reject")]
    [ProducesResponseType(typeof(LoanResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(Guid id)
    {
        _logger.LogInformation("Solicitud de rechazo para prestamo {LoanId}", id);
        try
        {
            var result = await _svc.RejectLoanAsync(id);
            _logger.LogInformation("Prestamo {LoanId} rechazado", id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Prestamo {LoanId} no encontrado para rechazar", id);
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "No se pudo rechazar prestamo {LoanId}: {Reason}", id, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }
}
