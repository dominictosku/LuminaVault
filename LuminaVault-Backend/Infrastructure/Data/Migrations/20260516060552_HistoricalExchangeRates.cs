using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LuminaVault.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class HistoricalExchangeRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExchangeRates_Currency",
                table: "ExchangeRates");

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveDate",
                table: "ExchangeRates",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2026, 5, 16, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_Currency_EffectiveDate",
                table: "ExchangeRates",
                columns: new[] { "Currency", "EffectiveDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExchangeRates_Currency_EffectiveDate",
                table: "ExchangeRates");

            migrationBuilder.DropColumn(
                name: "EffectiveDate",
                table: "ExchangeRates");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_Currency",
                table: "ExchangeRates",
                column: "Currency",
                unique: true);
        }
    }
}
