using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

public class ItemPhoto
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    [Required] public string FileName { get; set; } = "";
    [Required] public string ContentType { get; set; } = "";
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
