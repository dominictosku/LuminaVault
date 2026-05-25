using LuminaVault.Auth;
using LuminaVault.Hosting;

var builder = WebApplication.CreateBuilder(args);

var storage = builder.AddLuminaVaultStorage();
builder.Host.UseLuminaVaultSerilog(storage);

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 52_428_800); // 50 MB

var jwtStartupState = builder.Services.AddLuminaVaultAuth(builder.Configuration);
builder.Services.AddLuminaVaultPersistence(storage);
builder.Services.AddLuminaVaultWebDefaults();
builder.Services.AddLuminaVaultPriceProviders(builder.Configuration);
builder.Services.AddLuminaVaultFeatureServices(builder.Configuration);

var app = builder.Build();

app.ValidateJwtStartup(jwtStartupState);
app.MigrateAndSeedDatabase();
app.UseLuminaVaultRequestPipeline();
app.MapLuminaVaultEndpoints();

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
