using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using LuminaVault.Auth;
using LuminaVault.Data;
using LuminaVault.Endpoints;
using LuminaVault.Pricing;
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
        path: Path.Combine(ctx.HostingEnvironment.ContentRootPath, "logs", "luminavault-.log"),
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
});

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

app.MapGet("/", () => Results.Ok(new { app = "LuminaVault", version = "1.0" }));

app.MapAuth();
app.MapHouses();
app.MapFurniture();
app.MapItems();
app.MapPhotos();
app.MapAttachments();
app.MapFinance();
app.MapOdsData();
app.MapBackup();
app.MapSettings();

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
