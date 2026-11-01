using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

public class Item
{
    public int Id { get; set; }
    [Required, MaxLength(160)] public string Name { get; set; } = "";
    [MaxLength(80)] public string? Category { get; set; }
    public string? Description { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public decimal? Value { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public DateTime? WarrantyUntil { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Notes { get; set; }
    public string TagsCsv { get; set; } = "";

    public int? RoomId { get; set; }
    public Room? Room { get; set; }
    public int? FurnitureId { get; set; }
    public Furniture? Furniture { get; set; }
    public int? ContainerId { get; set; }
    public Container? Container { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool HasLithiumBattery { get; set; }
    public int? BatteryChargeIntervalMonths { get; set; } = 3;
    public DateTime? LastChargedAt { get; set; }

    public string? ModelFileName { get; set; }
    public string? ModelContentType { get; set; }

    public List<ItemPhoto> Photos { get; set; } = new();
    public List<DocumentAttachment> Attachments { get; set; } = new();

    public DateTime? GetNextBatteryChargeDue()
    {
        if (!HasLithiumBattery) return null;
        var interval = Math.Max(1, BatteryChargeIntervalMonths ?? 3);
        var baseline = LastChargedAt ?? PurchaseDate ?? CreatedAt;
        return baseline.AddMonths(interval);
    }

    public bool NeedsBatteryCharge(DateTime? asOfUtc = null)
    {
        if (!HasLithiumBattery) return false;
        var nextDue = GetNextBatteryChargeDue();
        if (!nextDue.HasValue) return false;
        var now = asOfUtc ?? DateTime.UtcNow;
        return now.Date >= nextDue.Value.Date;
    }
}
