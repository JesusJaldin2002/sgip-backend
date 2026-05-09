using FinTech.API.DTOs.Loans;
using FinTech.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FinTech.API.Controllers;

[ApiController]
[Route("api/loans")]
public class LoansController(ILoanService svc) : ControllerBase
{
    private readonly ILoanService _svc = svc;

    /// <summary>Simula un prestamo y retorna el cronograma sin guardar en BD.</summary>
    [HttpPost("simulate")]
    public async Task<IActionResult> Simulate([FromBody] SimulateLoanDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _svc.SimulateAsync(dto);
        return Ok(result);
    }

    /// <summary>Crea una solicitud de prestamo.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLoanDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var result = await _svc.CreateLoanAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Lista prestamos, opcionalmente filtrados por userId.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? userId)
    {
        var result = await _svc.GetLoansAsync(userId);
        return Ok(result);
    }

    /// <summary>Obtiene un prestamo por ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _svc.GetLoanByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Retorna el cronograma de pagos de un prestamo.</summary>
    [HttpGet("{id:guid}/schedule")]
    public async Task<IActionResult> GetSchedule(Guid id)
    {
        var result = await _svc.GetScheduleAsync(id);
        return Ok(result);
    }

    /// <summary>Aprueba un prestamo en estado Pending y crea la transaccion de desembolso.</summary>
    [HttpPatch("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id)
    {
        try
        {
            var result = await _svc.ApproveLoanAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Rechaza un prestamo en estado Pending.</summary>
    [HttpPatch("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id)
    {
        try
        {
            var result = await _svc.RejectLoanAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
