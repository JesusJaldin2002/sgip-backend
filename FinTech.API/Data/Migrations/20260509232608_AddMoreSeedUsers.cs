using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FinTech.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMoreSeedUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Loans",
                columns: new[] { "Id", "Amount", "CreatedAt", "InterestRate", "LoanType", "MonthlyIncome", "MonthlyPayment", "Status", "Term", "UpdatedAt", "UserId" },
                values: new object[,]
                {
                    { new Guid("33333333-3333-3333-3333-333333333333"), 15000m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.24m, "Fixed", 5000m, 570.57m, "Rejected", 36, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "user-003" },
                    { new Guid("44444444-4444-4444-4444-444444444444"), 3000m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.24m, "Fixed", 2000m, 280.36m, "Active", 12, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "user-004" },
                    { new Guid("55555555-5555-5555-5555-555555555555"), 9500m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.24m, "Fixed", 4500m, 623.07m, "Pending", 18, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "user-005" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Loans",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"));

            migrationBuilder.DeleteData(
                table: "Loans",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"));

            migrationBuilder.DeleteData(
                table: "Loans",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"));
        }
    }
}
