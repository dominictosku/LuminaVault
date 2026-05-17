using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

public class AssetCategory : INamedCategory
{
    public int Id { get; set; }
    [Required, MaxLength(80)] public string Name { get; set; } = "";
    [MaxLength(24)] public string Color { get; set; } = "#7c3aed";
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// Implemented by both AssetCategory and FinanceCategory so the settings endpoints
/// can share a single name-keyed CRUD implementation. Add fields here only if both
/// entities need them — anything resource-specific stays out.
public interface INamedCategory
{
    int Id { get; }
    string Name { get; set; }
    string Color { get; set; }
    int SortOrder { get; set; }
    DateTime CreatedAt { get; }
}
