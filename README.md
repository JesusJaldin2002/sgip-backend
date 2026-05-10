# SGIP — Backend API

Sistema de Gestión de Inversiones y Préstamos. API REST construida con .NET 10 y PostgreSQL que gestiona simulación de préstamos, flujo de aprobación y procesamiento de transacciones con garantía de idempotencia.

---

## Links de Despliegue

| Servicio | URL |
|---|---|
| **API (Railway)** | `https://sgip-backend-production.up.railway.app` |
| **Swagger UI** | `https://sgip-backend-production.up.railway.app/swagger` |

> No requiere autenticación. Usuarios de prueba: `user-001` a `user-005`.
>
> Seed data incluido: 5 préstamos en diferentes estados y 6 transacciones distribuidas entre `user-001`, `user-002` y `user-004`.

### Datos de seed por usuario

| Usuario | Préstamo | Transacciones |
|---|---|---|
| `user-001` | Activo — $5,000 / 12 meses | Disbursement Completed, Payment Completed, Payment Failed, Payment Pending |
| `user-002` | Pendiente — $8,000 / 24 meses | Transfer Completed |
| `user-003` | Rechazado — $2,000 | — |
| `user-004` | Activo — $3,000 | Disbursement Completed |
| `user-005` | Pendiente — $15,000 | — |

### Instrucciones de prueba (Swagger)

1. **Simular préstamo** — `POST /api/loans/simulate` con `userId`, `amount`, `term`, `monthlyIncome`, `loanType`
2. **Crear préstamo** — `POST /api/loans`; si `amount < $10,000` y el usuario tiene menos de 2 activos, se aprueba automáticamente y genera un desembolso
3. **Aprobar / rechazar** — `PATCH /api/loans/{id}/approve` o `/reject`; solo aplica a préstamos en estado `Pending` (`user-002`, `user-005`)
4. **Transacciones por usuario** — `GET /api/transactions?userId=user-001`; combinable con `?type=Payment&status=Completed`
5. **Idempotencia** — `POST /api/transactions` dos veces con el mismo `idempotencyKey`; la segunda devuelve la original sin duplicar

---

## Tecnologías Utilizadas

### Stack principal
- **.NET 10** / ASP.NET Core — framework web
- **PostgreSQL 14+** — base de datos relacional
- **Entity Framework Core 10 (Npgsql)** — ORM y migraciones
- **Serilog** — logging estructurado a consola y archivo rotativo diario

### Librerías principales

| Librería | Versión | Uso |
|---|---|---|
| Swashbuckle.AspNetCore | 10.1.7 | Documentación Swagger/OpenAPI |
| Npgsql.EFCore.PostgreSQL | 10.0.1 | Driver PostgreSQL para EF Core |
| Serilog.AspNetCore | 10.0.0 | Logging estructurado |
| xUnit + Moq | — | Tests unitarios e integración |

### Decisiones técnicas importantes
- **JsonStringEnumConverter** — los enums se serializan como strings (`"Fixed"`, `"Pending"`) para que el frontend no dependa de valores numéricos.
- **Migraciones automáticas en startup** — `db.Database.MigrateAsync()` al arrancar garantiza que Railway/Render aplique esquemas sin intervención manual.
- **Seed en runtime** — los cronogramas de pagos y transacciones de ejemplo se generan en `Program.cs` al iniciar, idempotentemente, sin depender de `HasData` para evitar migraciones innecesarias.

---

## Instalación Local

### Prerrequisitos
- .NET 10 SDK
- PostgreSQL 14+
- (Opcional) Docker y Docker Compose

### Opción A — Sin Docker

```bash
# 1. Clonar el repositorio
git clone https://github.com/TU-USUARIO/SGIP.git
cd SGIP

# 2. Configurar variable de entorno (o appsettings.Development.json)
$env:DATABASE_URL = "postgresql://usuario:password@localhost:5432/sgip"

# 3. Aplicar migraciones y levantar
dotnet run --project FinTech.API
```

Swagger disponible en: `http://localhost:5000/swagger`

### Opción B — Con Docker Compose

```bash
# Desde la raíz del proyecto
docker compose up --build -d

# Ver logs
docker compose logs -f api
```

El `docker-compose.yml` levanta la API y PostgreSQL juntos. Swagger disponible en `http://localhost:8080/swagger`.

---

## Variables de Entorno

| Variable | Descripción | Ejemplo |
|---|---|---|
| `DATABASE_URL` | URL de conexión PostgreSQL (formato Railway) | `postgresql://user:pass@host:5432/db` |
| `ASPNETCORE_ENVIRONMENT` | Entorno de ejecución | `Production` |
| `DB_NO_SSL` | Deshabilitar SSL (solo desarrollo local) | `true` |

---

## Testing

```bash
# Ejecutar todos los tests
dotnet test FinTech.Tests/FinTech.Tests.csproj

# Con reporte de cobertura
dotnet test FinTech.Tests/FinTech.Tests.csproj --collect:"XPlat Code Coverage"
```

**Tests incluidos (22 en total):**

| Suite | Tests | Qué verifica |
|---|---|---|
| `FinancialCalculatorTests` | 4 | Cuota fija ≈ $467 (±$1), count = term, saldo final = 0, TEM en rango |
| `LoanValidationTests` | 4 | Tasa minima/máxima, monto mínimo/máximo |
| `LoanServiceTests` | 10 | Max 3 préstamos activos, cap 40% ingreso, auto-aprobación, desembolso automático, aprobar/rechazar con validaciones |
| `TransactionIdempotencyTests` | 1 | Duplicate key retorna original y nunca llama CreateAsync |
| `TransactionStatusTests` | 3 | Pending → Completed (sin LoanId), Pending → Completed (LoanId válido), Pending → Failed (LoanId inexistente) |

---

## Arquitectura

### Estructura del proyecto

```
SGIP/
├── SGIP.slnx
├── FinTech.API/
│   ├── Controllers/          # Capa de presentación (HTTP)
│   ├── DTOs/                 # Request/Response objects
│   │   ├── Loans/
│   │   └── Transactions/
│   ├── Models/               # Entidades de dominio
│   │   └── Enums/
│   ├── Services/             # Lógica de negocio
│   │   ├── Interfaces/
│   │   └── Strategies/       # Strategy Pattern (Fixed/Decreasing)
│   ├── Repositories/         # Acceso a datos
│   │   ├── Interfaces/
│   │   └── Implementations/
│   ├── Data/                 # DbContext y Migraciones
│   │   └── Migrations/
│   ├── Utils/                # FinancialCalculator (puro, sin DI)
│   └── Program.cs
└── FinTech.Tests/            # xUnit + Moq
```

### Flujo de dependencias

```
Controllers → IService → IRepository → ApplicationDbContext
                      → FinancialCalculator (estático)
```

Los servicios nunca acceden al `DbContext` directamente — siempre a través de interfaces de repositorio.

### Patrones implementados

**1. Repository Pattern **
- Abstracción completa del acceso a datos mediante interfaces (`ILoanRepository`, `ITransactionRepository`).
- Los servicios dependen de interfaces, no de implementaciones concretas, lo que facilita el testing con mocks.

**2. Strategy Pattern**
- `ILoanCalculationStrategy` con dos implementaciones: `FixedLoanStrategy` (Sistema Francés) y `DecreasingLoanStrategy` (Sistema Alemán).
- Permite agregar nuevos tipos de amortización sin modificar `LoanService`.

---

## Decisiones de Diseño

**¿Por qué .NET 10 en lugar de .NET 8?**
El proyecto fue scaffoldeado con la versión más reciente disponible. .NET 10 es LTS-candidate y comparte API con .NET 8; la migración hacia atrás es trivial.

**¿Por qué EF Core con migraciones en lugar de Dapper?**
El modelo tiene relaciones (Loan → PaymentSchedule → Transaction) que se benefician del ORM. Las migraciones automáticas en startup simplifican el despliegue en Railway sin scripts SQL manuales.

**¿Qué trade-offs se hicieron?**
- Sin autenticación — `userId` es un string libre. Simplificación explícita del enunciado.
- Procesamiento de transacciones síncrono — en producción real existiría una cola de mensajes (RabbitMQ/SQS). Documentado en limitaciones.
- `PaymentSchedule.Status` (Pending/Paid) existe en el modelo pero no se actualiza al procesar pagos — requeriría vincular transacciones con cuotas específicas.

**¿Qué se simplificó y por qué?**
- Estado `Approved` del préstamo no se usa como estado intermedio — al aprobar manualmente se pasa directamente a `Active` para simplificar el flujo.
- No hay endpoint para cambiar estado de transacciones manualmente — la maquina de estados (Pending → Completed/Failed) es automática durante la creación.

---

## Supuestos y Limitaciones

### Funcionalidades no implementadas
- Autenticación y autorización (JWT/OAuth)
- Actualización manual del estado de transacciones vía endpoint
- Marcado de cuotas como `Paid` al registrar un pago
- Notificaciones (email/push) al cambiar estado de préstamo

### Simplificaciones realizadas
- `userId` como string libre sin validación de existencia
- Transacciones procesadas síncronamente (Pending → Completed/Failed en la misma request)
- Estado `Approved` existe en el enum pero el flujo va Pending → Active directamente

### Mejoras futuras
- Agregar autenticación con JWT
- Implementar procesamiento asíncrono de transacciones con cola de mensajes
- Vincular `Transaction.LoanId` con `PaymentSchedule` para marcar cuotas como pagadas
- Rate limiting y throttling en endpoints públicos

---

## Evidencia

### Vista general del Swagger UI
![Swagger Overview](./screenshots/swagger-overview.png)

### Simulación de préstamo — Request
![Simulate Request](./screenshots/swagger-simulate.png)

### Simulación de préstamo — Respuesta con cronograma
![Simulate Result](./screenshots/swagger-simulate-result.png)

### Creación de préstamo — Request
![Create Loan](./screenshots/swagger-create-loan.png)

### Creación de préstamo - Resultado Inválido por cantidad de meses menor a 6
![Create Loan Result](./screenshots/swagger-create-loan-result.png)

### Aprobación de préstamo
![Approve Loan](./screenshots/swagger-approve.png)

### Lista de transacciones — Request con filtros
![Transactions](./screenshots/swagger-transactions.png)

### Lista de transacciones — Resultado
![Transactions Result](./screenshots/swagger-transactions_result.png)

### Despliegue en Railway
![Railway Deploy](./screenshots/backend_despliegue.png)
