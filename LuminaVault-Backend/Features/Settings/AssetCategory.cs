using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

public class AssetCategory
{
    public int Id { get; set; }
    [Required, MaxLength(80)] public string Name { get; set; } = "";
    [MaxLength(24)] public string Color { get; set; } = "#7c3aed";
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
