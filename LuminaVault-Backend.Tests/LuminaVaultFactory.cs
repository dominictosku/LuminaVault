using LuminaVault.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LuminaVault.Tests;

/// One factory per test class — gives each test its own SQLite file under
/// the system temp dir, so tests don't share state. Disposed = file deleted.
public class LuminaVaultFactory : WebApplicationFactory<Program>
{
    private readonly string _environment;
    private readonly Dictionary<string, string?> _configuration;

    public readonly string DbPath = Path.Combine(
        Path.GetTempPath(),
        $"luminavault-test-{Guid.NewGuid():N}.db");

    // Shared-fixture test classes log in once per test, which would otherwise trip the
    // production login throttle. Raise it for the default factory; RateLimitTests opts back
    // into a low limit to exercise the throttle directly.
    public LuminaVaultFactory() : this("Development", new Dictionary<string, string?>
    {
        ["RateLimiting:LoginPermitLimit"] = "10000",
    })
    { }

    internal LuminaVaultFactory(
        string environment = "Development",
        Dictionary<string, string?>? configuration = null)
    {
        _environment = environment;
        _configuration = configuration ?? new Dictionary<string, string?>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        // UseSetting writes host configuration, which is visible to `builder.Configuration`
        // reads at service-registration time in Program.cs. ConfigureAppConfiguration is
        // layered in too late for options consumed during DI setup (e.g. rate-limit policies).
        foreach (var (key, value) in _configuration)
            builder.UseSetting(key, value);
        builder.ConfigureServices(services =>
        {
            var existing = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (existing is not null) services.Remove(existing);
            services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={DbPath}"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            var path = DbPath + suffix;
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best-effort cleanup */ }
        }
    }
}
