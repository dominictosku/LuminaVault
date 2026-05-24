using System.Data;
using System.IO.Compression;
using LuminaVault.Data;
using LuminaVault.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

internal class BackupService
{
    private readonly StoragePaths _storage;

    public BackupService(StoragePaths storage)
    {
        _storage = storage;
    }

    public async Task<byte[]> BuildBackupZip(AppDbContext db, CancellationToken ct)
    {
        var dbSnapshotPath = Path.Combine(Path.GetTempPath(), $"luminavault-backup-{Guid.NewGuid():N}.db");

        try
        {
            await CreateDatabaseSnapshot(db, dbSnapshotPath, ct);

            await using var buffer = new MemoryStream();
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                await AddFile(zip, dbSnapshotPath, "luminavault.db", ct);

                if (Directory.Exists(_storage.UploadsDirectory))
                {
                    foreach (var file in Directory.EnumerateFiles(_storage.UploadsDirectory, "*", SearchOption.AllDirectories))
                    {
                        var relative = Path.GetRelativePath(_storage.UploadsDirectory, file)
                            .Replace(Path.DirectorySeparatorChar, '/');
                        await AddFile(zip, file, $"uploads/{relative}", ct);
                    }
                }
            }

            return buffer.ToArray();
        }
        finally
        {
            TryDelete(dbSnapshotPath);
        }
    }

    public async Task<string> WriteScheduledBackup(
        AppDbContext db,
        BackupOptions options,
        CancellationToken ct)
    {
        var dir = Path.IsPathRooted(options.Directory)
            ? options.Directory
            : Path.Combine(_storage.DataDirectory, options.Directory);
        Directory.CreateDirectory(dir);

        var bytes = await BuildBackupZip(db, ct);
        var path = Path.Combine(dir, $"luminavault-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");
        await File.WriteAllBytesAsync(path, bytes, ct);
        PruneOldBackups(dir, options.RetainedFiles);
        return path;
    }

    private static async Task CreateDatabaseSnapshot(AppDbContext db, string targetPath, CancellationToken ct)
    {
        var sourceConnection = db.Database.GetDbConnection();
        if (sourceConnection is not SqliteConnection sqlite)
            throw new InvalidOperationException("Backups require a SQLite database connection.");

        var shouldClose = sourceConnection.State != ConnectionState.Open;
        if (shouldClose)
            await db.Database.OpenConnectionAsync(ct);

        try
        {
            var destinationConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = targetPath
            }.ToString();
            await using var destination = new SqliteConnection(destinationConnectionString);
            await destination.OpenAsync(ct);
            sqlite.BackupDatabase(destination);
        }
        finally
        {
            if (shouldClose)
                await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task AddFile(ZipArchive zip, string path, string entryName, CancellationToken ct)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
        await using var source = File.OpenRead(path);
        await using var target = entry.Open();
        await source.CopyToAsync(target, ct);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup for a temp snapshot; the backup itself has already succeeded or failed.
        }
    }

    private static void PruneOldBackups(string dir, int retainedFiles)
    {
        if (retainedFiles <= 0) return;
        var oldFiles = Directory.EnumerateFiles(dir, "luminavault-backup-*.zip")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.CreationTimeUtc)
            .Skip(retainedFiles);
        foreach (var file in oldFiles)
            file.Delete();
    }
}
