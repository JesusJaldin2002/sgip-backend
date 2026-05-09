using FinTech.API.Models;
using FinTech.API.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinTech.API.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<PaymentSchedule> PaymentSchedules => Set<PaymentSchedule>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Loan>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.Amount).HasPrecision(18, 2);
            e.Property(l => l.InterestRate).HasPrecision(8, 6);
            e.Property(l => l.MonthlyPayment).HasPrecision(18, 2);
            e.Property(l => l.MonthlyIncome).HasPrecision(18, 2);
            e.Property(l => l.LoanType).HasConversion<string>();
            e.Property(l => l.Status).HasConversion<string>();
        });

        modelBuilder.Entity<PaymentSchedule>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.TotalPayment).HasPrecision(18, 2);
            e.Property(p => p.Principal).HasPrecision(18, 2);
            e.Property(p => p.Interest).HasPrecision(18, 2);
            e.Property(p => p.RemainingBalance).HasPrecision(18, 2);
            e.Property(p => p.Status).HasConversion<string>();
            e.HasOne(p => p.Loan)
             .WithMany(l => l.PaymentSchedules)
             .HasForeignKey(p => p.LoanId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Transaction>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Amount).HasPrecision(18, 2);
            e.Property(t => t.Type).HasConversion<string>();
            e.Property(t => t.Status).HasConversion<string>();
            e.HasIndex(t => t.IdempotencyKey).IsUnique();
            e.HasOne(t => t.Loan)
             .WithMany(l => l.Transactions)
             .HasForeignKey(t => t.LoanId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);
        });

        var loan1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var loan2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var loan3Id = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var loan4Id = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var loan5Id = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var seedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Loan>().HasData(
            new Loan
            {
                Id = loan1Id,
                UserId = "user-001",
                Amount = 5000m,
                Term = 12,
                InterestRate = 0.24m,
                LoanType = LoanType.Fixed,
                Status = LoanStatus.Active,
                MonthlyPayment = 474.03m,
                MonthlyIncome = 3000m,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },
            new Loan
            {
                Id = loan2Id,
                UserId = "user-002",
                Amount = 8000m,
                Term = 24,
                InterestRate = 0.24m,
                LoanType = LoanType.Fixed,
                Status = LoanStatus.Pending,
                MonthlyPayment = 422.31m,
                MonthlyIncome = 4000m,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },
            // user-003: prestamo rechazado — para demostrar estado Rejected
            new Loan
            {
                Id = loan3Id,
                UserId = "user-003",
                Amount = 15000m,
                Term = 36,
                InterestRate = 0.24m,
                LoanType = LoanType.Fixed,
                Status = LoanStatus.Rejected,
                MonthlyPayment = 570.57m,
                MonthlyIncome = 5000m,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },
            // user-004: prestamo activo pequeno — para probar auto-aprobacion del segundo prestamo
            new Loan
            {
                Id = loan4Id,
                UserId = "user-004",
                Amount = 3000m,
                Term = 12,
                InterestRate = 0.24m,
                LoanType = LoanType.Fixed,
                Status = LoanStatus.Active,
                MonthlyPayment = 280.36m,
                MonthlyIncome = 2000m,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },
            // user-005: prestamo pendiente de monto alto — requiere aprobacion manual
            new Loan
            {
                Id = loan5Id,
                UserId = "user-005",
                Amount = 9500m,
                Term = 18,
                InterestRate = 0.24m,
                LoanType = LoanType.Fixed,
                Status = LoanStatus.Pending,
                MonthlyPayment = 623.07m,
                MonthlyIncome = 4500m,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            }
        );
    }
}
