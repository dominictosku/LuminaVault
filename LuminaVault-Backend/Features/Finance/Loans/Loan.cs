using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

public enum LoanStatus
{
    Active, PaidOff, Closed
}

/// A fixed-rate amortising loan (mortgage, car loan, personal loan). Stored
/// separately from FinanceAccount so the schedule and balance are computed
/// from a small, stable set of inputs and an optional extra-payment override.
///
/// Cash impact is not tied to a `FinanceAccount` directly — payments still
/// show up as Expense transactions for the cash side. AccountId here just
/// associates the loan with a liability account for net-worth reporting.
public class Loan
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    public string? Lender { get; set; }

    /// Optional liability account. When set, the loan's remaining balance
    /// is reflected by the linked account's negative balance.
    public int? AccountId { get; set; }
    public FinanceAccount? Account { get; set; }

    [Required, MaxLength(8)] public string Currency { get; set; } = "CHF";

    /// Original principal at origination.
    public decimal Principal { get; set; }

    /// Annual interest rate as a percentage (e.g. 4.25 for 4.25%).
    public decimal AnnualInterestRate { get; set; }

    /// Total term in months.
    public int TermMonths { get; set; }

    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;

    /// Optional extra principal paid every period on top of the scheduled payment.
    public decimal ExtraMonthlyPayment { get; set; }

    public LoanStatus Status { get; set; } = LoanStatus.Active;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
