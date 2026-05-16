using LuminaVault.Data;
using LuminaVault.Domain;
using LuminaVault.Validation;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

public record NotificationDto(
    int Id, string Type, NotificationSeverity Severity, string Title, string Message,
    string? Link, string Source, NotificationStatus Status,
    DateTime CreatedAt, DateTime? ReadAt, DateTime? DismissedAt);

public record NotificationCountsDto(int Unread, int Total);

public record NotificationScanResultDto(int Touched, DateTime ScannedAt);

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotifications(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").RequireAuthorization().WithTags("Notifications");

        group.MapGet("/", async (AppDbContext db, NotificationStatus? status, int limit = 200) =>
        {
            var query = db.Notifications.AsQueryable();
            if (status.HasValue) query = query.Where(n => n.Status == status.Value);
            else query = query.Where(n => n.Status != NotificationStatus.Dismissed);
            var rows = await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(Math.Clamp(limit, 1, 500))
                .ToListAsync();
            return Results.Ok(rows.Select(Map));
        });

        group.MapGet("/counts", async (AppDbContext db) =>
        {
            var unread = await db.Notifications.CountAsync(n => n.Status == NotificationStatus.Unread);
            var total = await db.Notifications.CountAsync(n => n.Status != NotificationStatus.Dismissed);
            return Results.Ok(new NotificationCountsDto(unread, total));
        });

        group.MapPost("/{id:int}/read", async (int id, AppDbContext db) =>
        {
            var n = await db.Notifications.FindAsync(id);
            if (n is null) return Problem.NotFound("Notification not found.");
            if (n.Status == NotificationStatus.Unread)
            {
                n.Status = NotificationStatus.Read;
                n.ReadAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            return Results.Ok(Map(n));
        });

        group.MapPost("/{id:int}/dismiss", async (int id, AppDbContext db) =>
        {
            var n = await db.Notifications.FindAsync(id);
            if (n is null) return Problem.NotFound("Notification not found.");
            n.Status = NotificationStatus.Dismissed;
            n.DismissedAt = DateTime.UtcNow;
            if (n.ReadAt is null) n.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(Map(n));
        });

        group.MapPost("/mark-all-read", async (AppDbContext db) =>
        {
            var now = DateTime.UtcNow;
            var unread = await db.Notifications.Where(n => n.Status == NotificationStatus.Unread).ToListAsync();
            foreach (var n in unread)
            {
                n.Status = NotificationStatus.Read;
                n.ReadAt = now;
            }
            if (unread.Count > 0) await db.SaveChangesAsync();
            return Results.Ok(new { updated = unread.Count });
        });

        group.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var n = await db.Notifications.FindAsync(id);
            if (n is null) return Problem.NotFound("Notification not found.");
            db.Notifications.Remove(n);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Manual trigger — useful for testing, for a "rescan now" button, and for
        // the integration tests which can't wait 6 hours for the background tick.
        group.MapPost("/scan", async (NotificationScanner scanner, AppDbContext db, CancellationToken ct) =>
        {
            var touched = await scanner.ScanAllAsync(db, ct);
            return Results.Ok(new NotificationScanResultDto(touched, DateTime.UtcNow));
        });

        return app;
    }

    static NotificationDto Map(Notification n) => new(
        n.Id, n.Type, n.Severity, n.Title, n.Message,
        n.Link, n.Source, n.Status,
        n.CreatedAt, n.ReadAt, n.DismissedAt);
}
