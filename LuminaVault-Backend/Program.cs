using System.Text;
using LuminaVault.Auth;
using LuminaVault.Data;
using LuminaVault.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddOpenApi();

var app = builder.Build();

// --- Migrate DB on start ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
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

app.Run();
