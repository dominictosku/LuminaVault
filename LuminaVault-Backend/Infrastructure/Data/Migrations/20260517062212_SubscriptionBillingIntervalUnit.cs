using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LuminaVault.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionBillingIntervalUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BillingIntervalCount",
                table: "Subscriptions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BillingIntervalUnit",
                table: "Subscriptions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Existing rows were treated as "every BillingIntervalDays days". Backfill
            // Unit=Day (0) + Count=BillingIntervalDays so monthly-cost and due-date math
            // produce the same answers they did before this migration.
            migrationBuilder.Sql(
                "UPDATE Subscriptions SET BillingIntervalUnit = 0, BillingIntervalCount = " +
                "CASE WHEN BillingIntervalDays > 0 THEN BillingIntervalDays ELSE 30 END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingIntervalCount",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "BillingIntervalUnit",
                table: "Subscriptions");
        }
    }
}
