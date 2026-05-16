using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

public enum NotificationSeverity { Info, Warning, Critical }

public enum NotificationStatus { Unread, Read, Dismissed }

public class Notification
{
    public int Id { get; set; }

    /// Rule type: "SubscriptionDue", "BudgetOverrun", "ForecastNegative", etc.
    [Required, MaxLength(64)] public string Type { get; set; } = "";

    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    [Required, MaxLength(160)] public string Title { get; set; } = "";
    [Required] public string Message { get; set; } = "";

    /// Optional SPA route to send the user to when they click the notification (e.g. "/subscriptions").
    [MaxLength(200)] public string? Link { get; set; }

    /// Stable key used to dedupe re-firing of the same alert. Example:
    /// "subscription:42:due:2026-05-20", "budget:Food:2026-05:overrun".
    /// Unique index in DB so a re-scan upserts cleanly.
    [Required, MaxLength(160)] public string Source { get; set; } = "";

    public NotificationStatus Status { get; set; } = NotificationStatus.Unread;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public DateTime? DismissedAt { get; set; }
}
