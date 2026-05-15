namespace LuminaVault.Domain;

public class MonthlyAccountSummary
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public FinanceAccount? Account { get; set; }
    public DateTime Month { get; set; } = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal? OpeningBalance { get; set; }
    public decimal? ClosingBalance { get; set; }
    public bool IsReconciled { get; set; }
    public DateTime? ReconciledAt { get; set; }
    public string? ReconciliationNotes { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
