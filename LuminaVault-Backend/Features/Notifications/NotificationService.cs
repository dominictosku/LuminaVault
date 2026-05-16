using LuminaVault.Data;
using LuminaVault.Domain;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

/// All notification mutations route through here so the Source-dedup invariant is
/// owned in one place. Scanners just describe what they detected; this service
/// decides whether to insert, update, or skip.
public class NotificationService
{
    /// Upsert by Source. If a notification with the same Source already exists and is
    /// still Unread/Read, we leave it alone (don't re-spam the user). If it was
    /// Dismissed and the condition is still true, we resurface it.
    public async Task<Notification> UpsertAsync(
        AppDbContext db,
        string source,
        string type,
        NotificationSeverity severity,
        string title,
        string message,
        string? link,
        CancellationToken ct = default)
    {
        var existing = await db.Notifications.FirstOrDefaultAsync(n => n.Source == source, ct);
        if (existing is not null)
        {
            if (existing.Status == NotificationStatus.Dismissed)
            {
                existing.Status = NotificationStatus.Unread;
                existing.DismissedAt = null;
                existing.ReadAt = null;
                existing.CreatedAt = DateTime.UtcNow;
            }
            // Refresh content so e.g. a budget-overrun message reflects the latest spend.
            existing.Title = title;
            existing.Message = message;
            existing.Link = link;
            existing.Severity = severity;
            await db.SaveChangesAsync(ct);
            return existing;
        }

        var notification = new Notification
        {
            Source = source,
            Type = type,
            Severity = severity,
            Title = title,
            Message = message,
            Link = link,
            Status = NotificationStatus.Unread,
            CreatedAt = DateTime.UtcNow,
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);
        return notification;
    }
}
