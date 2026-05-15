using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

/// Cross-cutting: an attachment belongs to either an inventory Item or a finance Subscription.
/// Lives under Inventory/Attachments/ because the model originated for item docs; the finance
/// use is a later addition and references this type via the Subscription navigation property.
public class DocumentAttachment
{
    public int Id { get; set; }
    public int? ItemId { get; set; }
    public Item? Item { get; set; }
    public int? SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }
    [Required, MaxLength(220)] public string OriginalFileName { get; set; } = "";
    [Required] public string FileName { get; set; } = "";
    [Required, MaxLength(120)] public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
