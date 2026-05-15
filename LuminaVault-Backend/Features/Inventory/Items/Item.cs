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

    public string? ModelFileName { get; set; }
    public string? ModelContentType { get; set; }

    public List<ItemPhoto> Photos { get; set; } = new();
    public List<DocumentAttachment> Attachments { get; set; } = new();
}
