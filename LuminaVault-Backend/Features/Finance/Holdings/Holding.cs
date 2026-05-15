using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

public class Holding
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public FinanceAccount? Account { get; set; }
    [Required, MaxLength(24)] public string Symbol { get; set; } = "";
    [MaxLength(120)] public string? Name { get; set; }
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public decimal? LastPrice { get; set; }
    public DateTime? LastPriceAt { get; set; }
    [MaxLength(80)] public string? ProviderId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
