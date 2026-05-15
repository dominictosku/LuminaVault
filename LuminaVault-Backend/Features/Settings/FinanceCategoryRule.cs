using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

public class FinanceCategoryRule
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string Pattern { get; set; } = "";
    [Required, MaxLength(80)] public string Category { get; set; } = "";
    public bool MatchPayee { get; set; } = true;
    public bool MatchDescription { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
