using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

public enum SavingsGoalStatus
{
    Active, Paused, Achieved, Cancelled
}

public class SavingsGoal
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    public int? AccountId { get; set; }
    public FinanceAccount? Account { get; set; }
    [Required, MaxLength(8)] public string Currency { get; set; } = "CHF";
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public DateTime? TargetDate { get; set; }
    public SavingsGoalStatus Status { get; set; } = SavingsGoalStatus.Active;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
