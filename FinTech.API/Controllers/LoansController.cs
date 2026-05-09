using FinTech.API.DTOs.Loans;
using FinTech.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FinTech.API.Controllers;

[ApiController]
[Route("api/loans")]
public class LoansController(ILoanService svc, ILogger<LoansController> logger) : ControllerBase
{
    private readonly ILoanService _svc = svc;
    private readonly ILogger<LoansController> _logger = logger;

    /// <summary>Simula un prestamo y retorna el cronograma sin guardar en BD.</summary>
    [HttpPost("simulate")]
    public async Task<IActionResult> Simulate([FromBody] SimulateLoanDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        _logger.LogInformation("Simulando prestamo: monto={Amount}, plazo={Term}, tipo={LoanType}", dto.Amount, dto.Term, dto.LoanType);
        var result = await _svc.SimulateAsync(dto);
        return Ok(result);
    }

    /// <summary>Crea una solicitud de prestamo.</summary>
    [HttpPost]
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
            _logger.LogWarning("Solicitud rechazada para usuario {UserId}: {Reason}", dto.UserId, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Lista prestamos, opcionalmente filtrados por userId.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? userId)
    {
        _logger.LogInformation("Listando prestamos (userId={UserId})", userId ?? "todos");
        var result = await _svc.GetLoansAsync(userId);
        return Ok(result);
    }

    /// <summary>Obtiene un prestamo por ID.</summary>
    [HttpGet("{id:guid}")]
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

    /// <summary>Retorna el cronograma de pagos de un prestamo.</summary>
    [HttpGet("{id:guid}/schedule")]
    public async Task<IActionResult> GetSchedule(Guid id)
    {
        _logger.LogInformation("Consultando cronograma del prestamo {LoanId}", id);
        var result = await _svc.GetScheduleAsync(id);
        return Ok(result);
    }

    /// <summary>Aprueba un prestamo en estado Pending y crea la transaccion de desembolso.</summary>
    [HttpPatch("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id)
    {
        _logger.LogInformation("Solicitud de aprobacion para prestamo {LoanId}", id);
        try
        {
            var result = await _svc.ApproveLoanAsync(id);
            _logger.LogInformation("Prestamo {LoanId} aprobado", id);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("Prestamo {LoanId} no encontrado para aprobar", id);
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("No se pudo aprobar prestamo {LoanId}: {Reason}", id, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Rechaza un prestamo en estado Pending.</summary>
    [HttpPatch("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id)
    {
        _logger.LogInformation("Solicitud de rechazo para prestamo {LoanId}", id);
        try
        {
            var result = await _svc.RejectLoanAsync(id);
            _logger.LogInformation("Prestamo {LoanId} rechazado", id);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("Prestamo {LoanId} no encontrado para rechazar", id);
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("No se pudo rechazar prestamo {LoanId}: {Reason}", id, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }
}
