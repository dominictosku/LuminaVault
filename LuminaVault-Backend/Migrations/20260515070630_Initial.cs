using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LuminaVault.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssetCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Institution = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    StartingBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceBudgets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Month = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LimitAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceBudgets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Houses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Houses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccountBalanceSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActualBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpectedBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Difference = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsReconciled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountBalanceSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountBalanceSnapshots_FinanceAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "FinanceAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FinanceTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    TransferAccountId = table.Column<int>(type: "INTEGER", nullable: true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    OccurredOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Payee = table.Column<string>(type: "TEXT", maxLength: 140, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    TagsCsv = table.Column<string>(type: "TEXT", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 24, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(28,8)", nullable: true),
                    PricePerUnit = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceTransactions_FinanceAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "FinanceAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FinanceTransactions_FinanceAccounts_TransferAccountId",
                        column: x => x.TransferAccountId,
                        principalTable: "FinanceAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Holdings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(28,8)", nullable: false),
                    AverageCost = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    LastPrice = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    LastPriceAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ProviderId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holdings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Holdings_FinanceAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "FinanceAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MonthlyAccountSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    Month = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Income = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Expenses = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ClosingBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsReconciled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReconciledAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReconciliationNotes = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyAccountSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonthlyAccountSummaries_FinanceAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "FinanceAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 140, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    BillingIntervalDays = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NextDueOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AutoRenew = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_FinanceAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "FinanceAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Color = table.Column<string>(type: "TEXT", nullable: false),
                    X = table.Column<double>(type: "REAL", nullable: false),
                    Z = table.Column<double>(type: "REAL", nullable: false),
                    Width = table.Column<double>(type: "REAL", nullable: false),
                    Depth = table.Column<double>(type: "REAL", nullable: false),
                    Height = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rooms_Houses_HouseId",
                        column: x => x.HouseId,
                        principalTable: "Houses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Furniture",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoomId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    X = table.Column<double>(type: "REAL", nullable: false),
                    Z = table.Column<double>(type: "REAL", nullable: false),
                    Y = table.Column<double>(type: "REAL", nullable: false),
                    Width = table.Column<double>(type: "REAL", nullable: false),
                    Depth = table.Column<double>(type: "REAL", nullable: false),
                    Height = table.Column<double>(type: "REAL", nullable: false),
                    RotationY = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Furniture", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Furniture_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Containers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FurnitureId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Containers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Containers_Furniture_FurnitureId",
                        column: x => x.FurnitureId,
                        principalTable: "Furniture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Brand = table.Column<string>(type: "TEXT", nullable: true),
                    Model = table.Column<string>(type: "TEXT", nullable: true),
                    SerialNumber = table.Column<string>(type: "TEXT", nullable: true),
                    Value = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    WarrantyUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    TagsCsv = table.Column<string>(type: "TEXT", nullable: false),
                    RoomId = table.Column<int>(type: "INTEGER", nullable: true),
                    FurnitureId = table.Column<int>(type: "INTEGER", nullable: true),
                    ContainerId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModelFileName = table.Column<string>(type: "TEXT", nullable: true),
                    ModelContentType = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Items_Containers_ContainerId",
                        column: x => x.ContainerId,
                        principalTable: "Containers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Items_Furniture_FurnitureId",
                        column: x => x.FurnitureId,
                        principalTable: "Furniture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Items_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DocumentAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemId = table.Column<int>(type: "INTEGER", nullable: true),
                    SubscriptionId = table.Column<int>(type: "INTEGER", nullable: true),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 220, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentAttachments_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentAttachments_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemPhotos_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalanceSnapshots_AccountId_SnapshotDate",
                table: "AccountBalanceSnapshots",
                columns: new[] { "AccountId", "SnapshotDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetCategories_Name",
                table: "AssetCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Containers_FurnitureId",
                table: "Containers",
                column: "FurnitureId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAttachments_ItemId",
                table: "DocumentAttachments",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAttachments_SubscriptionId",
                table: "DocumentAttachments",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBudgets_Category_Month",
                table: "FinanceBudgets",
                columns: new[] { "Category", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCategories_Name",
                table: "FinanceCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceTransactions_AccountId",
                table: "FinanceTransactions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceTransactions_Category",
                table: "FinanceTransactions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceTransactions_OccurredOn",
                table: "FinanceTransactions",
                column: "OccurredOn");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceTransactions_Symbol",
                table: "FinanceTransactions",
                column: "Symbol");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceTransactions_TransferAccountId",
                table: "FinanceTransactions",
                column: "TransferAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Furniture_RoomId",
                table: "Furniture",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Holdings_AccountId_Symbol",
                table: "Holdings",
                columns: new[] { "AccountId", "Symbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemPhotos_ItemId",
                table: "ItemPhotos",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_ContainerId",
                table: "Items",
                column: "ContainerId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_FurnitureId",
                table: "Items",
                column: "FurnitureId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_RoomId",
                table: "Items",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyAccountSummaries_AccountId_Month",
                table: "MonthlyAccountSummaries",
                columns: new[] { "AccountId", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_HouseId",
                table: "Rooms",
                column: "HouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_AccountId",
                table: "Subscriptions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_NextDueOn",
                table: "Subscriptions",
                column: "NextDueOn");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountBalanceSnapshots");

            migrationBuilder.DropTable(
                name: "AssetCategories");

            migrationBuilder.DropTable(
                name: "DocumentAttachments");

            migrationBuilder.DropTable(
                name: "FinanceBudgets");

            migrationBuilder.DropTable(
                name: "FinanceCategories");

            migrationBuilder.DropTable(
                name: "FinanceTransactions");

            migrationBuilder.DropTable(
                name: "Holdings");

            migrationBuilder.DropTable(
                name: "ItemPhotos");

            migrationBuilder.DropTable(
                name: "MonthlyAccountSummaries");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropTable(
                name: "FinanceAccounts");

            migrationBuilder.DropTable(
                name: "Containers");

            migrationBuilder.DropTable(
                name: "Furniture");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "Houses");
        }
    }
}
