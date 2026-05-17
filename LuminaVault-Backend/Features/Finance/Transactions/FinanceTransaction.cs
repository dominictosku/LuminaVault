using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

public enum FinanceTransactionKind
{
    Income, Expense, Transfer, Buy, Sell, Dividend, Fee
}

public enum FinanceTransactionStatus
{
    Pending, Cleared, Reconciled
}

public class FinanceTransaction
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public FinanceAccount? Account { get; set; }
    public int? TransferAccountId { get; set; }
    public FinanceAccount? TransferAccount { get; set; }

    public FinanceTransactionKind Kind { get; set; } = FinanceTransactionKind.Expense;
    public FinanceTransactionStatus Status { get; set; } = FinanceTransactionStatus.Cleared;
    public DateTime OccurredOn { get; set; } = DateTime.UtcNow.Date;
    [Required, MaxLength(140)] public string Payee { get; set; } = "";
    [Required, MaxLength(80)] public string Category { get; set; } = "General";
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string TagsCsv { get; set; } = "";

    // Trade fields — populated only for Buy / Sell / Dividend / Fee on investment & crypto accounts.
    [MaxLength(24)] public string? Symbol { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? PricePerUnit { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// Optional per-line breakdown for splitting one cash movement across categories
    /// (e.g. a grocery receipt that's part Food and part Household). Cash math still
    /// runs off Amount/Kind — splits only redirect category aggregation.
    public List<TransactionSplit> Splits { get; set; } = new();
}

public class TransactionSplit
{
    public int Id { get; set; }
    public int TransactionId { get; set; }
    public FinanceTransaction? Transaction { get; set; }
    [Required, MaxLength(80)] public string Category { get; set; } = "";
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
}
