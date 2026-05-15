using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LuminaVault.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinanceCategoryRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinanceCategoryRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Pattern = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    MatchPayee = table.Column<bool>(type: "INTEGER", nullable: false),
                    MatchDescription = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceCategoryRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCategoryRules_Pattern",
                table: "FinanceCategoryRules",
                column: "Pattern");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCategoryRules_Priority",
                table: "FinanceCategoryRules",
                column: "Priority");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinanceCategoryRules");
        }
    }
}
