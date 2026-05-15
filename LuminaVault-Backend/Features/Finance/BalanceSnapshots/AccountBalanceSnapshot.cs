namespace LuminaVault.Domain;

public class AccountBalanceSnapshot
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public FinanceAccount? Account { get; set; }
    public DateTime SnapshotDate { get; set; } = DateTime.UtcNow.Date;
    public decimal ActualBalance { get; set; }
    public decimal ExpectedBalance { get; set; }
    public decimal Difference { get; set; }
    public bool IsReconciled { get; set; } = true;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
