using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

public enum SubscriptionStatus
{
    Active, Paused, Cancelled
}

/// Calendar-aware billing period: lets a subscription say "every 1 month" instead
/// of "every 30 days" so monthly cost reporting and due-date math don't drift.
public enum BillingIntervalUnit
{
    Day, Week, Month, Year
}

public class Subscription
{
    public int Id { get; set; }
    [Required, MaxLength(140)] public string Name { get; set; } = "";
    [Required, MaxLength(80)] public string Category { get; set; } = "Subscriptions";
    [MaxLength(120)] public string? Provider { get; set; }
    public int? AccountId { get; set; }
    public FinanceAccount? Account { get; set; }
    public decimal Amount { get; set; }
    [Required, MaxLength(8)] public string Currency { get; set; } = "CHF";

    /// Semantic billing period unit; pairs with `BillingIntervalCount` (e.g. Month × 3 = quarterly).
    public BillingIntervalUnit BillingIntervalUnit { get; set; } = BillingIntervalUnit.Month;
    public int BillingIntervalCount { get; set; } = 1;

    /// Day-equivalent of the billing period, kept in sync with Unit/Count by the endpoints.
    /// Used by code paths that still want a "step by N days" approximation (ODS export, legacy
    /// imports). Authoritative for billing math is Unit + Count, not this field.
    public int BillingIntervalDays { get; set; } = 30;

    public DateTime StartedOn { get; set; } = DateTime.UtcNow.Date;
    public DateTime NextDueOn { get; set; } = DateTime.UtcNow.Date;
    public bool AutoRenew { get; set; } = true;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<DocumentAttachment> Attachments { get; set; } = new();
}
