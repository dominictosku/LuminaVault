using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

public enum SubscriptionStatus
{
    Active, Paused, Cancelled
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
