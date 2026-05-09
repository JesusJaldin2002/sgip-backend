using FinTech.API.DTOs.Transactions;
using FinTech.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FinTech.API.Controllers;

[ApiController]
[Route("api/transactions")]
public class TransactionsController(ITransactionService svc) : ControllerBase
{
    private readonly ITransactionService _svc = svc;

    /// <summary>Crea una transaccion. Si el IdempotencyKey ya existe, retorna la original.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTransactionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _svc.CreateTransactionAsync(dto);
        return Ok(result);
    }

    /// <summary>Lista transacciones con filtros opcionales por tipo y estado.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? type, [FromQuery] string? status)
    {
        var result = await _svc.GetTransactionsAsync(type, status);
        return Ok(result);
    }

    /// <summary>Obtiene una transaccion por ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _svc.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }
}
