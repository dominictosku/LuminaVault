using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

// Finance bounded context: accounts, transactions, holdings, monthly summaries,
// budgets, balance snapshots, subscriptions, and the user-managed category list.

public class FinanceCategory
{
    public int Id { get; set; }
    [Required, MaxLength(80)] public string Name { get; set; } = "";
    [MaxLength(24)] public string Color { get; set; } = "#7c3aed";
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum FinanceAccountType
{
    Checking, Savings, Cash, CreditCard, Investment, Crypto, Loan, Other
}

public enum FinanceTransactionKind
{
    Income, Expense, Transfer, Buy, Sell, Dividend, Fee
}

public enum FinanceTransactionStatus
{
    Pending, Cleared, Reconciled
}

public enum SubscriptionStatus
{
    Active, Paused, Cancelled
}

public class FinanceAccount
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(120)] public string? Institution { get; set; }
    public FinanceAccountType Type { get; set; } = FinanceAccountType.Checking;
    [Required, MaxLength(8)] public string Currency { get; set; } = "CHF";
    public decimal StartingBalance { get; set; }
    public decimal Balance { get; set; }
    [MaxLength(24)] public string Color { get; set; } = "#14b8a6";
    public string? Notes { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<FinanceTransaction> Transactions { get; set; } = new();
    public List<Subscription> Subscriptions { get; set; } = new();
    public List<AccountBalanceSnapshot> BalanceSnapshots { get; set; } = new();
    public List<Holding> Holdings { get; set; } = new();
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
}

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

public class Subscription
{
    public int Id { get; set; }
    [Required, MaxLength(140)] public string Name { get; set; } = "";
    [Required, MaxLength(80)] public string Category { get; set; } = "Subscriptions";
    [MaxLength(120)] public string? Provider { get; set; }
    public int? AccountId { get; set; }
    public FinanceAccount? Account { get; set; }
    public decimal Amount { get; set; }
    [Required, MaxLength(8)] public string Currency { get; set; } = "CHF";
    public int BillingIntervalDays { get; set; } = 30;
    public DateTime StartedOn { get; set; } = DateTime.UtcNow.Date;
    public DateTime NextDueOn { get; set; } = DateTime.UtcNow.Date;
    public bool AutoRenew { get; set; } = true;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<DocumentAttachment> Attachments { get; set; } = new();
}
