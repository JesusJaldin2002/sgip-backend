using FinTech.API.Data;
using FinTech.API.Models;
using FinTech.API.Models.Enums;
using FinTech.API.Repositories.Implementations;
using FinTech.API.Repositories.Interfaces;
using FinTech.API.Services;
using FinTech.API.Services.Interfaces;
using FinTech.API.Services.Strategies;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Text.Json.Serialization;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/sgip-.log", rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// Conexion a PostgreSQL
var connStr = builder.Configuration.GetConnectionString("DefaultConnection");

var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');
    var noSsl = Environment.GetEnvironmentVariable("DB_NO_SSL") == "true";
    var sslPart = noSsl ? "SSL Mode=Disable" : "SSL Mode=Require;Trust Server Certificate=true";
    connStr = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};{sslPart}";
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connStr));

// Repositorios
builder.Services.AddScoped<ILoanRepository, LoanRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();

// Servicios
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<ILoanService, LoanService>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "SGIP API",
        Version = "v1",
        Description = """
            Sistema de Gestion de Inversiones y Prestamos (SGIP).

            Reglas de negocio principales:
            - Monto: $500 – $50,000 | Plazo: 6 – 60 meses | TEA: 18% – 35% (default 24%)
            - Max 3 prestamos activos por usuario
            - La suma de cuotas no puede exceder el 40% del ingreso mensual
            - Auto-aprobacion: monto < $10,000 y menos de 2 prestamos activos
            - Idempotencia en transacciones via IdempotencyKey unico
            """
    });
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// Aplicar migraciones y generar cronogramas faltantes al arrancar
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await SeedSchedulesAsync(db);
    await SeedTransactionsAsync(db);
}

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SGIP API v1"));

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();

// Genera cronogramas de pago para prestamos que no los tienen (seed data o importaciones)
static async Task SeedSchedulesAsync(ApplicationDbContext db)
{
    var loansWithoutSchedule = await db.Loans
        .Where(l => !db.PaymentSchedules.Any(p => p.LoanId == l.Id))
        .ToListAsync();

    if (loansWithoutSchedule.Count == 0) return;

    Log.Information("Generando cronogramas para {Count} prestamo(s) sin schedule", loansWithoutSchedule.Count);

    foreach (var loan in loansWithoutSchedule)
    {
        ILoanCalculationStrategy strategy = loan.LoanType == LoanType.Decreasing
            ? new DecreasingLoanStrategy()
            : new FixedLoanStrategy();

        var entries = strategy.GenerateSchedule(loan.Amount, loan.InterestRate, loan.Term, loan.CreatedAt);

        db.PaymentSchedules.AddRange(entries.Select(e => new PaymentSchedule
        {
            LoanId = loan.Id,
            PaymentNumber = e.PaymentNumber,
            DueDate = e.DueDate,
            TotalPayment = e.TotalPayment,
            Principal = e.Principal,
            Interest = e.Interest,
            RemainingBalance = e.RemainingBalance
        }));
    }

    await db.SaveChangesAsync();
    Log.Information("Cronogramas generados correctamente");
}

// Inserta transacciones de ejemplo solo si no existen (idempotente por clave)
static async Task SeedTransactionsAsync(ApplicationDbContext db)
{
    const string marker = "seed-transactions-v2";
    if (await db.Transactions.AnyAsync(t => t.IdempotencyKey == marker)) return;

    Log.Information("Insertando transacciones de ejemplo (v2 con userId)");

    var loan1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    var loan4Id = Guid.Parse("44444444-4444-4444-4444-444444444444");
    var seedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    db.Transactions.AddRange(
        // Desembolso automatico al aprobar el prestamo de user-001
        new Transaction
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            IdempotencyKey = marker,
            UserId = "user-001",
            Type = TransactionType.Disbursement,
            Amount = 5000m,
            Status = TransactionStatus.Completed,
            LoanId = loan1Id,
            Description = "Desembolso del prestamo 11111111",
            CreatedAt = seedDate
        },
        // Pago completado de cuota mensual de user-001
        new Transaction
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            IdempotencyKey = "seed-payment-loan1-cuota1-v2",
            UserId = "user-001",
            Type = TransactionType.Payment,
            Amount = 474.03m,
            Status = TransactionStatus.Completed,
            LoanId = loan1Id,
            Description = "Pago cuota 1",
            CreatedAt = seedDate.AddMonths(1)
        },
        // Pago fallido de user-001
        new Transaction
        {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            IdempotencyKey = "seed-payment-failed-v2",
            UserId = "user-001",
            Type = TransactionType.Payment,
            Amount = 474.03m,
            Status = TransactionStatus.Failed,
            LoanId = null,
            Description = "Pago fallido — prestamo no encontrado",
            CreatedAt = seedDate.AddMonths(1).AddDays(3)
        },
        // Transferencia completada de user-002
        new Transaction
        {
            Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            IdempotencyKey = "seed-transfer-001-v2",
            UserId = "user-002",
            Type = TransactionType.Transfer,
            Amount = 200m,
            Status = TransactionStatus.Completed,
            LoanId = null,
            Description = "Transferencia entre cuentas",
            CreatedAt = seedDate.AddMonths(1).AddDays(5)
        },
        // Pago pendiente de user-001
        new Transaction
        {
            Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            IdempotencyKey = "seed-payment-pending-v2",
            UserId = "user-001",
            Type = TransactionType.Payment,
            Amount = 474.03m,
            Status = TransactionStatus.Pending,
            LoanId = loan1Id,
            Description = "Pago en proceso",
            CreatedAt = seedDate.AddMonths(2)
        },
        // Desembolso del prestamo de user-004
        new Transaction
        {
            Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            IdempotencyKey = "seed-disbursement-loan4-v2",
            UserId = "user-004",
            Type = TransactionType.Disbursement,
            Amount = 3000m,
            Status = TransactionStatus.Completed,
            LoanId = loan4Id,
            Description = "Desembolso del prestamo 44444444",
            CreatedAt = seedDate
        }
    );

    await db.SaveChangesAsync();
    Log.Information("Transacciones de ejemplo insertadas correctamente");
}
