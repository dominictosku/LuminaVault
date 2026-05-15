using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

public enum FinanceAccountType
{
    Checking, Savings, Cash, CreditCard, Investment, Crypto, Loan, Other
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
