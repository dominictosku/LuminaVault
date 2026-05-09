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
    AddColumnIfMissing("Items", "ModelFileName", "TEXT NULL");
    AddColumnIfMissing("Items", "ModelContentType", "TEXT NULL");

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

    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_FinanceTransactions_OccurredOn ON FinanceTransactions (OccurredOn)");
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_FinanceTransactions_Category ON FinanceTransactions (Category)");
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_FinanceTransactions_AccountId ON FinanceTransactions (AccountId)");
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_FinanceTransactions_TransferAccountId ON FinanceTransactions (TransferAccountId)");
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_Subscriptions_NextDueOn ON Subscriptions (NextDueOn)");
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_Subscriptions_AccountId ON Subscriptions (AccountId)");
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
app.MapFinance();

app.Run();
