using LuminaVault.Data;

namespace LuminaVault.Endpoints;

public static class BackupEndpoints
{
    public static IEndpointRouteBuilder MapBackup(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/data").RequireAuthorization().WithTags("Data");

        g.MapGet("/backup", async (
            AppDbContext db,
            BackupService backup,
            CancellationToken ct) =>
        {
            var bytes = await backup.BuildBackupZip(db, ct);
            var fileName = $"luminavault-backup-{DateTime.UtcNow:yyyy-MM-dd}.zip";
            return Results.File(bytes, "application/zip", fileName);
        }).WithRequestTimeout("backup");

        return app;
    }
}
