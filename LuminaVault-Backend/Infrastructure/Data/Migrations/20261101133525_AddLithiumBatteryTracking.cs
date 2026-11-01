using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LuminaVault.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLithiumBatteryTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BatteryChargeIntervalMonths",
                table: "Items",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasLithiumBattery",
                table: "Items",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastChargedAt",
                table: "Items",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatteryChargeIntervalMonths",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "HasLithiumBattery",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "LastChargedAt",
                table: "Items");
        }
    }
}
