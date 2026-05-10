using System.Text;
using System.Text.Json.Serialization;
using LuminaVault.Auth;
using LuminaVault.Data;
using LuminaVault.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 52_428_800); // 50 MB

// --- Config ---
var jwtOpt = new JwtOptions();
builder.Configuration.GetSection("Jwt").Bind(jwtOpt);
if (string.IsNullOrWhiteSpace(jwtOpt.Key))
    jwtOpt.Key = Environment.GetEnvironmentVariable("LUMINA_JWT_KEY")
                 ?? "dev-only-key-change-me-please-this-must-be-32+chars-long!!";
builder.Services.AddSingleton(jwtOpt);
builder.Services.AddSingleton<JwtService>();

// --- DB ---
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "luminavault.db");
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite($"Data Source={dbPath}"));

// --- Auth ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwtOpt.Issuer,
            ValidAudience = jwtOpt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOpt.Key)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });
builder.Services.AddAuthorization();

// --- CORS for Angular dev server ---
const string DevCors = "DevCors";
builder.Services.AddCors(o => o.AddPolicy(DevCors, p => p
    .WithOrigins("http://localhost:4200", "https://localhost:4200")
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.Configure<JsonOptions>(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddOpenApi();

var app = builder.Build();

// --- Migrate DB on start ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Idempotent column adds for evolving schema (dev-friendly migrations).
    void AddColumnIfMissing(string table, string column, string definition)
    {
        var exists = db.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM pragma_table_info({table}) WHERE name = {column}")
            .AsEnumerable().FirstOrDefault();
        if (exists == 0)
        {
#pragma warning disable EF1002 // table/column/definition are hardcoded literals from the caller, not user input
            db.Database.ExecuteSqlRaw($"ALTER TABLE {table} ADD COLUMN {column} {definition}");
#pragma warning restore EF1002
        }
    }
    AddColumnIfMissing("Items", "RoomId", "INTEGER NULL REFERENCES Rooms(Id) ON DELETE SET NULL");
    AddColumnIfMissing("Items", "Category", "TEXT NULL");
    AddColumnIfMissing("Items", "ModelFileName", "TEXT NULL");
    AddColumnIfMissing("Items", "ModelContentType", "TEXT NULL");
    AddColumnIfMissing("MonthlyAccountSummaries", "IsReconciled", "INTEGER NOT NULL DEFAULT 0");
    AddColumnIfMissing("MonthlyAccountSummaries", "ReconciledAt", "TEXT NULL");
    AddColumnIfMissing("MonthlyAccountSummaries", "ReconciliationNotes", "TEXT NULL");

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS DocumentAttachments (
            Id INTEGER NOT NULL CONSTRAINT PK_DocumentAttachments PRIMARY KEY AUTOINCREMENT,
            ItemId INTEGER NULL,
            SubscriptionId INTEGER NULL,
            OriginalFileName TEXT NOT NULL,
            FileName TEXT NOT NULL,
            ContentType TEXT NOT NULL,
            Size INTEGER NOT NULL,
            UploadedAt TEXT NOT NULL,
            CONSTRAINT FK_DocumentAttachments_Items_ItemId FOREIGN KEY (ItemId) REFERENCES Items (Id) ON DELETE CASCADE,
            CONSTRAINT FK_DocumentAttachments_Subscriptions_SubscriptionId FOREIGN KEY (SubscriptionId) REFERENCES Subscriptions (Id) ON DELETE CASCADE
        );
        """);
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_DocumentAttachments_ItemId ON DocumentAttachments (ItemId)");
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_DocumentAttachments_SubscriptionId ON DocumentAttachments (SubscriptionId)");

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS AssetCategories (
            Id INTEGER NOT NULL CONSTRAINT PK_AssetCategories PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Color TEXT NOT NULL,
            SortOrder INTEGER NOT NULL,
            CreatedAt TEXT NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_AssetCategories_Name ON AssetCategories (Name)");

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS FinanceCategories (
            Id INTEGER NOT NULL CONSTRAINT PK_FinanceCategories PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Color TEXT NOT NULL,
            SortOrder INTEGER NOT NULL,
            CreatedAt TEXT NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_FinanceCategories_Name ON FinanceCategories (Name)");

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS FinanceAccounts (
            Id INTEGER NOT NULL CONSTRAINT PK_FinanceAccounts PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Institution TEXT NULL,
            Type INTEGER NOT NULL,
            Currency TEXT NOT NULL,
            StartingBalance TEXT NOT NULL,
            Balance TEXT NOT NULL,
            Color TEXT NOT NULL,
            Notes TEXT NULL,
            IsArchived INTEGER NOT NULL,
            CreatedAt TEXT NOT NULL
        );
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS FinanceTransactions (
            Id INTEGER NOT NULL CONSTRAINT PK_FinanceTransactions PRIMARY KEY AUTOINCREMENT,
            AccountId INTEGER NOT NULL,
            TransferAccountId INTEGER NULL,
            Kind INTEGER NOT NULL,
            Status INTEGER NOT NULL,
            OccurredOn TEXT NOT NULL,
            Payee TEXT NOT NULL,
            Category TEXT NOT NULL,
            Amount TEXT NOT NULL,
            Description TEXT NULL,
            Notes TEXT NULL,
            TagsCsv TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL,
            CONSTRAINT FK_FinanceTransactions_FinanceAccounts_AccountId FOREIGN KEY (AccountId) REFERENCES FinanceAccounts (Id) ON DELETE CASCADE,
            CONSTRAINT FK_FinanceTransactions_FinanceAccounts_TransferAccountId FOREIGN KEY (TransferAccountId) REFERENCES FinanceAccounts (Id) ON DELETE SET NULL
        );
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS Subscriptions (
            Id INTEGER NOT NULL CONSTRAINT PK_Subscriptions PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Category TEXT NOT NULL,
            Provider TEXT NULL,
            AccountId INTEGER NULL,
            Amount TEXT NOT NULL,
            Currency TEXT NOT NULL,
            BillingIntervalDays INTEGER NOT NULL,
            StartedOn TEXT NOT NULL,
            NextDueOn TEXT NOT NULL,
            AutoRenew INTEGER NOT NULL,
            Status INTEGER NOT NULL,
            Notes TEXT NULL,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL,
            CONSTRAINT FK_Subscriptions_FinanceAccounts_AccountId FOREIGN KEY (AccountId) REFERENCES FinanceAccounts (Id) ON DELETE SET NULL
        );
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS MonthlyAccountSummaries (
            Id INTEGER NOT NULL CONSTRAINT PK_MonthlyAccountSummaries PRIMARY KEY AUTOINCREMENT,
            AccountId INTEGER NOT NULL,
            Month TEXT NOT NULL,
            Income TEXT NOT NULL,
            Expenses TEXT NOT NULL,
            OpeningBalance TEXT NULL,
            ClosingBalance TEXT NULL,
            Notes TEXT NULL,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL,
            CONSTRAINT FK_MonthlyAccountSummaries_FinanceAccounts_AccountId FOREIGN KEY (AccountId) REFERENCES FinanceAccounts (Id) ON DELETE CASCADE
        );
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS FinanceBudgets (
            Id INTEGER NOT NULL CONSTRAINT PK_FinanceBudgets PRIMARY KEY AUTOINCREMENT,
            Category TEXT NOT NULL,
            Month TEXT NOT NULL,
            LimitAmount TEXT NOT NULL,
            Notes TEXT NULL,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        );
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS AccountBalanceSnapshots (
            Id INTEGER NOT NULL CONSTRAINT PK_AccountBalanceSnapshots PRIMARY KEY AUTOINCREMENT,
            AccountId INTEGER NOT NULL,
            SnapshotDate TEXT NOT NULL,
            ActualBalance TEXT NOT NULL,
            ExpectedBalance TEXT NOT NULL,
            Difference TEXT NOT NULL,
            IsReconciled INTEGER NOT NULL,
            Notes TEXT NULL,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL,
            CONSTRAINT FK_AccountBalanceSnapshots_FinanceAccounts_AccountId FOREIGN KEY (AccountId) REFERENCES FinanceAccounts (Id) ON DELETE CASCADE
        );
        """);

    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_FinanceTransactions_OccurredOn ON FinanceTransactions (OccurredOn)");
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_FinanceTransactions_Category ON FinanceTransactions (Category)");
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_FinanceTransactions_AccountId ON FinanceTransactions (AccountId)");
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_FinanceTransactions_TransferAccountId ON FinanceTransactions (TransferAccountId)");
    db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_MonthlyAccountSummaries_AccountId_Month ON MonthlyAccountSummaries (AccountId, Month)");
    db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_FinanceBudgets_Category_Month ON FinanceBudgets (Category, Month)");
    db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_AccountBalanceSnapshots_AccountId_SnapshotDate ON AccountBalanceSnapshots (AccountId, SnapshotDate)");
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_Subscriptions_NextDueOn ON Subscriptions (NextDueOn)");
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_Subscriptions_AccountId ON Subscriptions (AccountId)");

    if (!db.AssetCategories.Any())
    {
        var defaults = new[] { "IT", "Hobby", "Möbel", "Werkzeug", "Fahrzeug", "Bürobedarf", "Kleidung", "Schule", "Reinigung", "Homelab", "Sonstiges" };
        for (var i = 0; i < defaults.Length; i++)
            db.AssetCategories.Add(new LuminaVault.Domain.AssetCategory { Name = defaults[i], SortOrder = i, Color = "#7c3aed" });
        db.SaveChanges();
    }

    if (!db.FinanceCategories.Any())
    {
        var defaults = new[] { "Salary", "Food", "Housing", "Transport", "Health", "Career", "Hobby", "Savings", "Investments", "Subscriptions", "Insurance", "Utilities", "Obligatorisch", "Körper" };
        for (var i = 0; i < defaults.Length; i++)
            db.FinanceCategories.Add(new LuminaVault.Domain.FinanceCategory { Name = defaults[i], SortOrder = i, Color = "#7c3aed" });
        db.SaveChanges();
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(DevCors);
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { app = "LuminaVault", version = "1.0" }));

app.MapAuth();
app.MapHouses();
app.MapFurniture();
app.MapItems();
app.MapPhotos();
app.MapAttachments();
app.MapFinance();
app.MapOdsData();
app.MapSettings();

app.Run();
