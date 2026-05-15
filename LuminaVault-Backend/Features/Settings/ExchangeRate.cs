using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

public class ExchangeRate
{
    public int Id { get; set; }
    [Required, MaxLength(8)] public string Currency { get; set; } = "";
    public decimal RateToBase { get; set; } = 1m;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
