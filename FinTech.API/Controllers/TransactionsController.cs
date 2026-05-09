using FinTech.API.DTOs.Transactions;
using FinTech.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FinTech.API.Controllers;

[ApiController]
[Route("api/transactions")]
public class TransactionsController(ITransactionService svc, ILogger<TransactionsController> logger) : ControllerBase
{
    private readonly ITransactionService _svc = svc;
    private readonly ILogger<TransactionsController> _logger = logger;

    /// <summary>Crea una transaccion. Si el IdempotencyKey ya existe, retorna la original.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTransactionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        _logger.LogInformation("Solicitud de transaccion recibida: tipo={Type}, monto={Amount}, key={Key}", dto.Type, dto.Amount, dto.IdempotencyKey);
        var result = await _svc.CreateTransactionAsync(dto);
        _logger.LogInformation("Transaccion {TransactionId} procesada", result.Id);
        return Ok(result);
    }

    /// <summary>Lista transacciones con filtros opcionales por tipo y estado.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? type, [FromQuery] string? status)
    {
        _logger.LogInformation("Listando transacciones (tipo={Type}, estado={Status})", type ?? "todos", status ?? "todos");
        var result = await _svc.GetTransactionsAsync(type, status);
        return Ok(result);
    }

    /// <summary>Obtiene una transaccion por ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        _logger.LogInformation("Consultando transaccion {TransactionId}", id);
        var result = await _svc.GetByIdAsync(id);
        if (result == null)
        {
            _logger.LogWarning("Transaccion {TransactionId} no encontrada", id);
            return NotFound();
        }
        return Ok(result);
    }
}
