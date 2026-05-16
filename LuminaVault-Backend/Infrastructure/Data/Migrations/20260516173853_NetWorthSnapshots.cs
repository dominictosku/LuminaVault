using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LuminaVault.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class NetWorthSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NetWorthSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SnapshotDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AccountNetWorth = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HoldingsMarketValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HoldingsCostBasis = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InventoryValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetWorth = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetWorthSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NetWorthSnapshots_SnapshotDate",
                table: "NetWorthSnapshots",
                column: "SnapshotDate",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NetWorthSnapshots");
        }
    }
}
