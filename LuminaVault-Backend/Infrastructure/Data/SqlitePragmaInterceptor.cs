using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LuminaVault.Data;

/// Sets per-connection SQLite PRAGMAs every time EF opens a pooled connection.
///
/// Why these specifically:
///   - `busy_timeout=5000` — without it, a concurrent writer (two browser tabs, a backup
///     curl racing a save) gets an immediate SQLITE_BUSY rather than waiting. 5s is the
///     SQLite docs' suggested ceiling for desktop apps.
///   - `journal_mode=WAL` — concurrent readers don't block writers. Persists in the DB
///     file once set, but re-asserting on every open is cheap and self-healing if the
///     file ever gets reverted to rollback journal.
///   - `foreign_keys=ON` — SQLite ships with FK enforcement *off* by default. Without
///     this, our ON DELETE CASCADE chains don't fire and the DB can accumulate orphans.
internal sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => ApplyPragmas(connection);

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ApplyPragmas(connection);
        return Task.CompletedTask;
    }

    static void ApplyPragmas(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA busy_timeout=5000; PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();
    }
}
