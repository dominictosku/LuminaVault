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

const string DevFallbackJwtKey = "dev-only-key-change-me-please-this-must-be-32+chars-long!!";

var builder = WebApplication.CreateBuilder(args);

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
app.MapSettings();

app.Run();

// Expose the implicit Program class so WebApplicationFactory<Program> in the test project
// can boot the same composition root.
public partial class Program;
