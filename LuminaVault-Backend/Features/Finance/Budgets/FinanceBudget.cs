using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

public class FinanceBudget
{
    public int Id { get; set; }
    [Required, MaxLength(80)] public string Category { get; set; } = "General";
    public DateTime Month { get; set; } = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
    public decimal LimitAmount { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
