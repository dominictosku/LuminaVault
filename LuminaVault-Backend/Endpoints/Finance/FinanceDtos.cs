using LuminaVault.Domain;

namespace LuminaVault.Endpoints;

// Wire-format records (DTOs + Inputs) for the finance resource family.
// Stays in the LuminaVault.Endpoints namespace so existing using-directives don't change.

public record FinanceAccountDto(
    int Id, string Name, string? Institution, FinanceAccountType Type, string Currency,
    decimal StartingBalance, decimal Balance, string Color, string? Notes,
    bool IsArchived, DateTime CreatedAt);

public record FinanceAccountInput(
    string Name, string? Institution, FinanceAccountType Type, string Currency,
    decimal StartingBalance, decimal Balance, string Color, string? Notes, bool IsArchived);

public record FinanceTransactionDto(
    int Id, int AccountId, string? AccountName, int? TransferAccountId, string? TransferAccountName,
    FinanceTransactionKind Kind, FinanceTransactionStatus Status, DateTime OccurredOn,
    string Payee, string Category, decimal Amount, string? Description, string? Notes,
    string[] Tags, string? Symbol, decimal? Quantity, decimal? PricePerUnit,
    DateTime CreatedAt, DateTime UpdatedAt);

public record FinanceTransactionInput(
    int AccountId, int? TransferAccountId, FinanceTransactionKind Kind, FinanceTransactionStatus Status,
    DateTime OccurredOn, string Payee, string Category, decimal Amount,
    string? Description, string? Notes, string[] Tags,
    string? Symbol, decimal? Quantity, decimal? PricePerUnit);

public record HoldingDto(
    int Id, int AccountId, string? AccountName, string Currency, string Symbol, string? Name,
    decimal Quantity, decimal AverageCost, decimal? LastPrice, DateTime? LastPriceAt,
    string? ProviderId, decimal CostBasis, decimal? MarketValue, decimal? UnrealizedPnL,
    decimal? UnrealizedPnLPercent, string? Notes, DateTime CreatedAt, DateTime UpdatedAt);

public record HoldingPriceInput(decimal? LastPrice, string? Name, string? ProviderId, string? Notes);

public record MonthlyAccountSummaryDto(
    int Id, int AccountId, string? AccountName, string Currency, DateTime Month,
    decimal Income, decimal Expenses, decimal Net, decimal? OpeningBalance,
    decimal? ClosingBalance, decimal? ExpectedClosingBalance, decimal? ClosingDifference,
    bool IsReconciled, DateTime? ReconciledAt, string? ReconciliationNotes,
    string? Notes, DateTime CreatedAt, DateTime UpdatedAt);

public record MonthlyAccountSummaryInput(
    int AccountId, DateTime Month, decimal Income, decimal Expenses,
    decimal? OpeningBalance, decimal? ClosingBalance, string? Notes);

public record MonthlyReconciliationInput(bool IsReconciled, string? Notes);

public record SubscriptionDto(
    int Id, string Name, string Category, string? Provider, int? AccountId, string? AccountName,
    decimal Amount, string Currency, int BillingIntervalDays, DateTime StartedOn,
    DateTime NextDueOn, bool AutoRenew, SubscriptionStatus Status, string? Notes,
    decimal MonthlyAmount, DocumentAttachmentDto[] Attachments, DateTime CreatedAt, DateTime UpdatedAt);

public record SubscriptionInput(
    string Name, string Category, string? Provider, int? AccountId, decimal Amount, string Currency,
    int BillingIntervalDays, DateTime StartedOn, DateTime NextDueOn, bool AutoRenew,
    SubscriptionStatus Status, string? Notes);

public record FinanceBudgetDto(
    int Id, string Category, DateTime Month, decimal LimitAmount, decimal Spent,
    decimal Remaining, decimal UsedPercent, string? Notes, DateTime CreatedAt, DateTime UpdatedAt);

public record FinanceBudgetInput(string Category, DateTime Month, decimal LimitAmount, string? Notes);

public record AccountBalanceSnapshotDto(
    int Id, int AccountId, string? AccountName, string Currency, DateTime SnapshotDate,
    decimal ActualBalance, decimal ExpectedBalance, decimal Difference, bool IsReconciled,
    string? Notes, DateTime CreatedAt, DateTime UpdatedAt);

public record AccountBalanceSnapshotInput(
    int AccountId, DateTime SnapshotDate, decimal ActualBalance, bool IsReconciled, string? Notes);

public record SubscriptionGenerateTransactionInput(FinanceTransactionStatus Status, bool AdvanceNextDueOn);
