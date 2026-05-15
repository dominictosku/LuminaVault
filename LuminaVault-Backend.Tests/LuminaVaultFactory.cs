using LuminaVault.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LuminaVault.Tests;

/// One factory per test class — gives each test its own SQLite file under
/// the system temp dir, so tests don't share state. Disposed = file deleted.
public class LuminaVaultFactory : WebApplicationFactory<Program>
{
    public readonly string DbPath = Path.Combine(
        Path.GetTempPath(),
        $"luminavault-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
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
