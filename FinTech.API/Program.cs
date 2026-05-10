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
