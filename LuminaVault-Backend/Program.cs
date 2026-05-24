using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using LuminaVault.Auth;
using LuminaVault.Data;
using LuminaVault.Endpoints;
using LuminaVault.Pricing;
using LuminaVault.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
// `Log` from Serilog is no longer referenced after dropping the bootstrap logger pattern,
// but Serilog (the namespace) is still needed for UseSerilog and UseSerilogRequestLogging.

const string DevFallbackJwtKey = "dev-only-key-change-me-please-this-must-be-32+chars-long!!";

var builder = WebApplication.CreateBuilder(args);

// Resolve where stateful files (DB, uploads, logs) live. LUMINA_DATA_DIR lets containers
// point everything at a single mounted volume; local dev uses ./data under the backend.
var dataDirOverride = Environment.GetEnvironmentVariable("LUMINA_DATA_DIR");
var storage = StorageBootstrap.Resolve(builder.Environment.ContentRootPath, dataDirOverride);
builder.Services.AddSingleton(storage);

// Replace the default ILogger pipeline with Serilog reading from configuration.
// Hosts can override sinks/levels via appsettings.json without code changes.
//
// We deliberately don't use Serilog's CreateBootstrapLogger pattern here: it sets a
// global frozen-once logger, which breaks WebApplicationFactory<Program> tests that
// boot the host multiple times in a single process ("The logger is already frozen").
builder.Host.UseSerilog((ctx, services, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(storage.LogsDirectory, "luminavault-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}"));

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 52_428_800); // 50 MB

// --- Config ---
var jwtOpt = new JwtOptions();
builder.Configuration.GetSection("Jwt").Bind(jwtOpt);
var jwtKeyFromEnv = false;
if (string.IsNullOrWhiteSpace(jwtOpt.Key))
{
    var envKey = Environment.GetEnvironmentVariable("LUMINA_JWT_KEY");
    if (!string.IsNullOrWhiteSpace(envKey))
    {
        jwtOpt.Key = envKey;
        jwtKeyFromEnv = true;
    }
    else
    {
        jwtOpt.Key = DevFallbackJwtKey;
    }
}
builder.Services.AddSingleton(jwtOpt);
builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

// --- DB ---
// Tracking stays on by default so the common "load by id (incl. FindAsync), mutate,
// save" pattern keeps working. Hot read endpoints opt out per-query with
// `.AsNoTracking()` — see TransactionEndpoints, FinanceSummaryEndpoints, etc.
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite($"Data Source={storage.DatabasePath}")
       .AddInterceptors(new SqlitePragmaInterceptor()));

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

// --- Price providers ---
// Each provider self-declares which account types it supports via IPriceProvider.Supports().
// Add additional providers (Finnhub, AlphaVantage, etc.) by implementing IPriceProvider and
// registering them here — PriceProviderService dispatches by account type.
builder.Services.AddHttpClient<CoinGeckoPriceProvider>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["PriceProviders:CoinGecko:BaseUrl"]
        ?? "https://api.coingecko.com/api/v3/");
    c.Timeout = TimeSpan.FromSeconds(20);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("LuminaVault/1.0");
    var apiKey = builder.Configuration["PriceProviders:CoinGecko:ApiKey"];
    if (!string.IsNullOrWhiteSpace(apiKey))
        c.DefaultRequestHeaders.Add("x-cg-demo-api-key", apiKey);
});
builder.Services.AddTransient<IPriceProvider>(sp => sp.GetRequiredService<CoinGeckoPriceProvider>());

builder.Services.AddHttpClient<FinnhubPriceProvider>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["PriceProviders:Finnhub:BaseUrl"]
        ?? "https://finnhub.io/api/v1/");
    c.Timeout = TimeSpan.FromSeconds(20);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("LuminaVault/1.0");
});
builder.Services.AddTransient<IPriceProvider>(sp => sp.GetRequiredService<FinnhubPriceProvider>());

builder.Services.AddScoped<PriceProviderService>();

// --- Request timeouts ---
// Caps how long an individual request can run before we cancel it. Upload-heavy paths
// (ODS import, glTF model upload, backup zip stream) get explicit policies; everything
// else uses the default 30s. Without this, a malformed 49 MB ODS could pin a Kestrel
// thread for minutes parsing XML.
builder.Services.AddRequestTimeouts(o =>
{
    o.DefaultPolicy = new Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(30),
    };
    o.AddPolicy("upload", TimeSpan.FromSeconds(60));
    // Backup zips the entire uploads/ folder + DB; can be hundreds of MB on a well-used
    // instance, and the user is actively waiting on it. Give it room to breathe.
    o.AddPolicy("backup", TimeSpan.FromMinutes(5));
});

// --- Rate limiting ---
// Holdings refresh fans out to Finnhub (60/min free tier) and CoinGecko (no key needed
// but courteous limits). A clicky user can drain the day's quota in seconds — cap per JWT subject.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("price-refresh", httpContext =>
    {
        var key = httpContext.User.Identity?.Name
                  ?? httpContext.Connection.RemoteIpAddress?.ToString()
                  ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 6,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });
    o.AddPolicy("auth", httpContext =>
    {
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });
});

// --- Scheduled backups ---
// The manual /api/data/backup endpoint is always available. The hosted service
// no-ops unless Backup:Enabled=true.
var backupOptions = new BackupOptions();
builder.Configuration.GetSection("Backup").Bind(backupOptions);
builder.Services.AddSingleton(backupOptions);
builder.Services.AddSingleton<BackupService>();
builder.Services.AddHostedService<BackupBackgroundService>();

// --- Subscription automation ---
// Manual generation is available through /api/finance/subscriptions/generate-due.
// The hosted service only runs when SubscriptionAutomation:Enabled=true.
var subscriptionAutomationOptions = new SubscriptionAutomationOptions();
builder.Configuration.GetSection("SubscriptionAutomation").Bind(subscriptionAutomationOptions);
builder.Services.AddSingleton(subscriptionAutomationOptions);
builder.Services.AddScoped<SubscriptionAutomationService>();
builder.Services.AddHostedService<SubscriptionAutomationBackgroundService>();

// --- Net worth snapshots ---
// Captures aggregate net worth (cash + holdings market value + inventory) on a daily cron
// so the dashboard can render a historical equity curve. Manual capture is available via
// POST /api/finance/net-worth/snapshot. Hosted service no-ops when NetWorth:Enabled=false.
var netWorthOptions = new NetWorthOptions();
builder.Configuration.GetSection("NetWorth").Bind(netWorthOptions);
builder.Services.AddSingleton(netWorthOptions);
builder.Services.AddScoped<NetWorthSnapshotService>();
builder.Services.AddHostedService<NetWorthBackgroundService>();

builder.Services.AddScoped<CashFlowForecastService>();

// --- Notifications ---
// Periodic scan of subscriptions/budgets/forecast against alert rules. Dedupe is
// keyed off Notification.Source so re-runs upsert instead of spamming.
var notificationOptions = new NotificationOptions();
builder.Configuration.GetSection("Notifications").Bind(notificationOptions);
builder.Services.AddSingleton(notificationOptions);
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<NotificationScanner>();
builder.Services.AddHostedService<NotificationBackgroundService>();

builder.Services.AddOpenApi();

var app = builder.Build();

// Loud warning if the dev fallback JWT key slipped into a non-dev environment.
if (jwtOpt.Key == DevFallbackJwtKey)
{
    var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    if (!app.Environment.IsDevelopment())
    {
        startupLogger.LogError(
            "LuminaVault is running with the built-in dev JWT key in a non-Development environment. " +
            "Set Jwt:Key in appsettings.json or the LUMINA_JWT_KEY env var before exposing this instance.");
    }
    else
    {
        startupLogger.LogWarning(
            "Using built-in dev JWT key. Set Jwt:Key or LUMINA_JWT_KEY for any non-local use.");
    }
}
else if (jwtKeyFromEnv)
{
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup")
        .LogInformation("JWT signing key loaded from LUMINA_JWT_KEY env var.");
}

// --- Migrate & seed DB on start ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    Seeder.Seed(db);
}

// Catch unhandled exceptions and return the same `{ error: "msg" }` envelope used
// by validation failures, so the Angular client's `e?.error?.error` handling works
// uniformly. The real exception is already logged by UseSerilogRequestLogging below
// (and re-logged here at Error level with the full stack).
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async ctx =>
    {
        var feature = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var ex = feature?.Error;
        if (ex is not null)
        {
            var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("UnhandledException");
            logger.LogError(ex, "Unhandled exception while processing {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
        }
        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        ctx.Response.ContentType = "application/json";
        // Production callers see a generic message; Development gets the exception text
        // to make debugging from the network panel cheap.
        var message = app.Environment.IsDevelopment() && ex is not null
            ? ex.Message
            : "Something went wrong. Check the server logs.";
        await ctx.Response.WriteAsJsonAsync(new { error = message });
    });
});

// Single request-completed log line per HTTP request with method/path/status/elapsed.
// Cheaper and tidier than the default per-stage AspNetCore logger.
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(DevCors);
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseRequestTimeouts();

app.MapGet("/", () => Results.Ok(new { app = "LuminaVault", version = "1.0" }));

// Healthcheck for reverse proxies, uptime monitors, and `docker healthcheck`.
// Verifies the DB is reachable so a stale-NFS / locked-file scenario is reported as unhealthy
// rather than as "we're up, but every request returns 500".
app.MapGet("/api/health", async (AppDbContext db, CancellationToken ct) =>
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
        return Results.Ok(new { status = "ok", database = "ok" });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "degraded", database = "error", message = ex.Message },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
})
.WithTags("Health")
.AllowAnonymous();

app.MapAuth();
app.MapHouses();
app.MapFurniture();
app.MapItems();
app.MapPhotos();
app.MapAttachments();
app.MapFinance();
app.MapOdsData();
app.MapBankCsvData();
app.MapBackup();
app.MapSettings();
app.MapNotifications();

try
{
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // Without the bootstrap logger we don't have a global Log.Logger to call into,
    // but the host's ILoggerFactory is still alive at this point.
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup")
        .LogCritical(ex, "LuminaVault terminated unexpectedly");
    throw;
}

// Expose the implicit Program class so WebApplicationFactory<Program> in the test project
// can boot the same composition root.
public partial class Program;
