using FinTech.API.DTOs.Transactions;
using FinTech.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FinTech.API.Controllers;

/// <summary>Gestion de transacciones con garantia de idempotencia.</summary>
[ApiController]
[Route("api/transactions")]
[Produces("application/json")]
public class TransactionsController(ITransactionService svc, ILogger<TransactionsController> logger) : ControllerBase
{
    private readonly ITransactionService _svc = svc;
    private readonly ILogger<TransactionsController> _logger = logger;

    /// <summary>Crea una transaccion con garantia de idempotencia.</summary>
    /// <remarks>
    /// Si el <c>IdempotencyKey</c> ya existe en el sistema, retorna la transaccion original
    /// sin crear una nueva entrada. Esto garantiza que reintentos o doble-clicks no generen
    /// transacciones duplicadas.
    ///
    /// **Maquina de estados:** La transaccion se crea en estado <c>Pending</c> y transiciona
    /// automaticamente a:
    /// - <c>Completed</c>: si el procesamiento es exitoso (LoanId valido o ausente).
    /// - <c>Failed</c>: si el <c>LoanId</c> proporcionado no corresponde a ningun prestamo existente.
    ///
    /// **Tipos soportados:** Disbursement, Payment, Transfer.
    /// Los desembolsos (<c>Disbursement</c>) se generan automaticamente al aprobar un prestamo.
    /// </remarks>
    /// <param name="dto">Datos de la transaccion: IdempotencyKey, tipo, monto, LoanId opcional y descripcion opcional.</param>
    [HttpPost]
    [ProducesResponseType(typeof(TransactionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTransactionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        _logger.LogInformation("Solicitud de transaccion recibida: tipo={Type}, monto={Amount}, key={Key}", dto.Type, dto.Amount, dto.IdempotencyKey);
        var result = await _svc.CreateTransactionAsync(dto);
        _logger.LogInformation("Transaccion {TransactionId} procesada con estado {Status}", result.Id, result.Status);
        return Ok(result);
    }

    /// <summary>Lista transacciones con filtros opcionales por tipo y estado.</summary>
    /// <remarks>
    /// Los filtros son acumulativos (AND). Si se omiten ambos, retorna todas las transacciones
    /// ordenadas por fecha descendente.
    ///
    /// Valores validos para <c>type</c>: <c>Disbursement</c>, <c>Payment</c>, <c>Transfer</c>.
    ///
    /// Valores validos para <c>status</c>: <c>Pending</c>, <c>Completed</c>, <c>Failed</c>.
    /// </remarks>
    /// <param name="type">Tipo de transaccion. Opcional.</param>
    /// <param name="status">Estado de la transaccion. Opcional.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TransactionResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? type, [FromQuery] string? status)
    {
        _logger.LogInformation("Listando transacciones (tipo={Type}, estado={Status})", type ?? "todos", status ?? "todos");
        var result = await _svc.GetTransactionsAsync(type, status);
        return Ok(result);
    }

    /// <summary>Obtiene una transaccion especifica por su ID.</summary>
    /// <param name="id">GUID de la transaccion.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TransactionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
