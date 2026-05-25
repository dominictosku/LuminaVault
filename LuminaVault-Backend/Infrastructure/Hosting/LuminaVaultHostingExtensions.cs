using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using LuminaVault.Data;
using LuminaVault.Endpoints;
using LuminaVault.Pricing;
using LuminaVault.Storage;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

namespace LuminaVault.Hosting;

public static class LuminaVaultHostingExtensions
{
    private const string DevCors = "DevCors";

    public static StoragePaths AddLuminaVaultStorage(this WebApplicationBuilder builder)
    {
        var dataDirOverride = Environment.GetEnvironmentVariable("LUMINA_DATA_DIR");
        var storage = StorageBootstrap.Resolve(builder.Environment.ContentRootPath, dataDirOverride);
        builder.Services.AddSingleton(storage);
        builder.Services.AddSingleton<UploadStorage>();
        return storage;
    }

    public static IHostBuilder UseLuminaVaultSerilog(this IHostBuilder host, StoragePaths storage) =>
        host.UseSerilog((ctx, services, cfg) => cfg
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

    public static IServiceCollection AddLuminaVaultPersistence(
        this IServiceCollection services,
        StoragePaths storage)
    {
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlite($"Data Source={storage.DatabasePath}")
                .AddInterceptors(new SqlitePragmaInterceptor()));
        return services;
    }

    public static IServiceCollection AddLuminaVaultWebDefaults(this IServiceCollection services)
    {
        services.AddCors(o => o.AddPolicy(DevCors, p => p
            .WithOrigins("http://localhost:4200", "https://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()));

        services.Configure<JsonOptions>(o =>
        {
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        services.AddRequestTimeouts(o =>
        {
            o.DefaultPolicy = new Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromSeconds(30),
            };
            o.AddPolicy("upload", TimeSpan.FromSeconds(60));
            o.AddPolicy("backup", TimeSpan.FromMinutes(5));
        });

        services.AddRateLimiter(o =>
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

        services.AddOpenApi();
        return services;
    }

    public static IServiceCollection AddLuminaVaultPriceProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpClient<CoinGeckoPriceProvider>(c =>
        {
            c.BaseAddress = new Uri(configuration["PriceProviders:CoinGecko:BaseUrl"]
                ?? "https://api.coingecko.com/api/v3/");
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("LuminaVault/1.0");
            var apiKey = configuration["PriceProviders:CoinGecko:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
                c.DefaultRequestHeaders.Add("x-cg-demo-api-key", apiKey);
        });
        services.AddTransient<IPriceProvider>(sp => sp.GetRequiredService<CoinGeckoPriceProvider>());

        services.AddHttpClient<FinnhubPriceProvider>(c =>
        {
            c.BaseAddress = new Uri(configuration["PriceProviders:Finnhub:BaseUrl"]
                ?? "https://finnhub.io/api/v1/");
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("LuminaVault/1.0");
        });
        services.AddTransient<IPriceProvider>(sp => sp.GetRequiredService<FinnhubPriceProvider>());

        services.AddScoped<PriceProviderService>();
        return services;
    }

    public static IServiceCollection AddLuminaVaultFeatureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var backupOptions = new BackupOptions();
        configuration.GetSection("Backup").Bind(backupOptions);
        services.AddSingleton(backupOptions);
        services.AddSingleton<BackupService>();
        services.AddHostedService<BackupBackgroundService>();

        var subscriptionAutomationOptions = new SubscriptionAutomationOptions();
        configuration.GetSection("SubscriptionAutomation").Bind(subscriptionAutomationOptions);
        services.AddSingleton(subscriptionAutomationOptions);
        services.AddScoped<SubscriptionAutomationService>();
        services.AddHostedService<SubscriptionAutomationBackgroundService>();

        var netWorthOptions = new NetWorthOptions();
        configuration.GetSection("NetWorth").Bind(netWorthOptions);
        services.AddSingleton(netWorthOptions);
        services.AddScoped<NetWorthSnapshotService>();
        services.AddHostedService<NetWorthBackgroundService>();

        services.AddScoped<CashFlowForecastService>();

        var notificationOptions = new NotificationOptions();
        configuration.GetSection("Notifications").Bind(notificationOptions);
        services.AddSingleton(notificationOptions);
        services.AddScoped<NotificationService>();
        services.AddScoped<NotificationScanner>();
        services.AddHostedService<NotificationBackgroundService>();

        return services;
    }

    public static void MigrateAndSeedDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
        Seeder.Seed(db);
    }

    public static void UseLuminaVaultRequestPipeline(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async ctx =>
            {
                var feature = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
                var ex = feature?.Error;
                if (ex is not null)
                {
                    var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("UnhandledException");
                    logger.LogError(ex, "Unhandled exception while processing {Method} {Path}",
                        ctx.Request.Method, ctx.Request.Path);
                }

                ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
                ctx.Response.ContentType = "application/json";
                var message = app.Environment.IsDevelopment() && ex is not null
                    ? ex.Message
                    : "Something went wrong. Check the server logs.";
                await ctx.Response.WriteAsJsonAsync(new { error = message });
            });
        });

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
    }

    public static WebApplication MapLuminaVaultEndpoints(this WebApplication app)
    {
        app.MapSystem();
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
        return app;
    }
}
