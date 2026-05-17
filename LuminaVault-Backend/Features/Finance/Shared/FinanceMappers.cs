using LuminaVault.Domain;
using static LuminaVault.Endpoints.FinanceHelpers;

namespace LuminaVault.Endpoints;

/// Entity → DTO mappers for the finance resource family. Kept separate from helpers
/// so a glance at this file shows the wire shape, while behavior lives in FinanceHelpers.
internal static class FinanceMappers
{
    public static FinanceAccountDto MapAccount(FinanceAccount account) =>
        new(account.Id, account.Name, account.Institution, account.Type, account.Currency,
            account.StartingBalance, account.Balance, account.Color, account.Notes,
            account.IsArchived, account.CreatedAt);

    public static FinanceTransactionDto MapTransaction(FinanceTransaction transaction) =>
        new(transaction.Id, transaction.AccountId, transaction.Account?.Name,
            transaction.TransferAccountId, transaction.TransferAccount?.Name,
            transaction.Kind, transaction.Status, transaction.OccurredOn,
            transaction.Payee, transaction.Category, transaction.Amount,
            transaction.Description, transaction.Notes,
            string.IsNullOrWhiteSpace(transaction.TagsCsv) ? Array.Empty<string>() : transaction.TagsCsv.Split(','),
            transaction.Symbol, transaction.Quantity, transaction.PricePerUnit,
            transaction.Splits
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
                .Select(s => new TransactionSplitDto(s.Id, s.Category, s.Amount, s.Notes, s.SortOrder))
                .ToArray(),
            transaction.CreatedAt, transaction.UpdatedAt);

    public static HoldingDto MapHolding(Holding holding, HoldingPerformance? performance = null)
    {
        var costBasis = holding.Quantity * holding.AverageCost;
        decimal? marketValue = holding.LastPrice.HasValue ? holding.Quantity * holding.LastPrice.Value : null;
        decimal? pnl = marketValue.HasValue ? marketValue.Value - costBasis : null;
        decimal? pnlPercent = pnl.HasValue && costBasis > 0
            ? Math.Round(pnl.Value / costBasis * 100m, 2)
            : null;
        var realized = performance?.RealizedPnL ?? 0m;
        var dividends = performance?.Dividends ?? 0m;
        var fees = performance?.Fees ?? 0m;
        var totalReturn = realized + dividends - fees + (pnl ?? 0m);
        return new HoldingDto(
            holding.Id, holding.AccountId, holding.Account?.Name,
            holding.Account?.Currency ?? "CHF",
            holding.Symbol, holding.Name,
            holding.Quantity, holding.AverageCost,
            holding.LastPrice, holding.LastPriceAt,
            holding.ProviderId, costBasis, marketValue, pnl, pnlPercent,
            realized, dividends, fees, totalReturn,
            holding.Notes, holding.CreatedAt, holding.UpdatedAt);
    }

    public static MonthlyAccountSummaryDto MapMonthlySummary(MonthlyAccountSummary monthlySummary, decimal expectedClosingBalance)
    {
        var difference = monthlySummary.ClosingBalance.HasValue
            ? monthlySummary.ClosingBalance.Value - expectedClosingBalance
            : (decimal?)null;
        return new(monthlySummary.Id, monthlySummary.AccountId, monthlySummary.Account?.Name,
            monthlySummary.Account?.Currency ?? "CHF", monthlySummary.Month,
            monthlySummary.Income, monthlySummary.Expenses, monthlySummary.Income - monthlySummary.Expenses,
            monthlySummary.OpeningBalance, monthlySummary.ClosingBalance, expectedClosingBalance, difference,
            monthlySummary.IsReconciled, monthlySummary.ReconciledAt, monthlySummary.ReconciliationNotes,
            monthlySummary.Notes, monthlySummary.CreatedAt, monthlySummary.UpdatedAt);
    }

    public static SubscriptionDto MapSubscription(Subscription subscription) =>
        new(subscription.Id, subscription.Name, subscription.Category, subscription.Provider,
            subscription.AccountId, subscription.Account?.Name, subscription.Amount, subscription.Currency,
            subscription.BillingIntervalDays, subscription.StartedOn, subscription.NextDueOn,
            subscription.AutoRenew, subscription.Status, subscription.Notes,
            ToMonthlyAmount(subscription.Amount, subscription.BillingIntervalDays),
            subscription.Attachments.Select(AttachmentEndpoints.MapAttachment).ToArray(),
            subscription.CreatedAt, subscription.UpdatedAt);

    public static FinanceBudgetDto MapBudget(FinanceBudget budget, decimal spent)
    {
        var remaining = budget.LimitAmount - spent;
        var used = budget.LimitAmount <= 0 ? 0 : Math.Round(spent / budget.LimitAmount * 100, 1);
        return new FinanceBudgetDto(budget.Id, budget.Category, budget.Month, budget.LimitAmount,
            spent, remaining, used, budget.Notes, budget.CreatedAt, budget.UpdatedAt);
    }

    public static SavingsGoalDto MapGoal(SavingsGoal goal)
    {
        var remaining = Math.Max(0, goal.TargetAmount - goal.CurrentAmount);
        var progress = goal.TargetAmount <= 0 ? 0 : Math.Round(goal.CurrentAmount / goal.TargetAmount * 100m, 1);
        return new SavingsGoalDto(
            goal.Id, goal.Name, goal.AccountId, goal.Account?.Name, goal.Currency,
            goal.TargetAmount, goal.CurrentAmount, remaining, progress,
            goal.TargetDate, goal.Status, goal.Notes, goal.CreatedAt, goal.UpdatedAt);
    }

    public static AccountBalanceSnapshotDto MapBalanceSnapshot(AccountBalanceSnapshot snapshot) =>
        new(snapshot.Id, snapshot.AccountId, snapshot.Account?.Name, snapshot.Account?.Currency ?? "CHF",
            snapshot.SnapshotDate, snapshot.ActualBalance, snapshot.ExpectedBalance, snapshot.Difference,
            snapshot.IsReconciled, snapshot.Notes, snapshot.CreatedAt, snapshot.UpdatedAt);
}
