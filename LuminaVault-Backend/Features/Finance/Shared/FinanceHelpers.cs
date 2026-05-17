using LuminaVault.Data;
using LuminaVault.Domain;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

/// Shared helpers used across the finance endpoint files.
///
/// `RecalculateBalances` is the load-bearing one: it walks every account's snapshots,
/// summaries, and unsummarized transactions to recompute the cached `Balance` column.
/// It's called from write endpoints (transaction insert/update/delete, summary changes,
/// reconcile, snapshot ops) — never from reads.
internal static class FinanceHelpers
{
    public static async Task LoadTransactionRefs(AppDbContext db, FinanceTransaction transaction)
    {
        await db.Entry(transaction).Reference(t => t.Account).LoadAsync();
        await db.Entry(transaction).Reference(t => t.TransferAccount).LoadAsync();
    }

    public static async Task RecalculateBalances(AppDbContext db)
    {
        var accounts = await db.FinanceAccounts.ToListAsync();
        if (accounts.Count == 0) return;

        var snapshots = await db.AccountBalanceSnapshots
            .Where(s => s.IsReconciled)
            .ToListAsync();
        var latestSnapshots = snapshots
            .GroupBy(s => s.AccountId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.SnapshotDate).First());

        var balances = accounts.ToDictionary(
            a => a.Id,
            a => latestSnapshots.TryGetValue(a.Id, out var snapshot)
                ? snapshot.ActualBalance
                : a.StartingBalance);
        var summaries = await db.MonthlyAccountSummaries.ToListAsync();
        var summaryKeys = summaries.Select(s => SummaryKey(s.AccountId, s.Month)).ToHashSet();
        var tx = await db.FinanceTransactions
            .Where(t => t.Status != FinanceTransactionStatus.Pending)
            .ToListAsync();
        foreach (var t in tx)
        {
            if (balances.ContainsKey(t.AccountId) &&
                IsAfterLatestSnapshot(t.AccountId, t.OccurredOn, latestSnapshots) &&
                !HasSummaryFor(t.AccountId, t.OccurredOn, summaryKeys))
            {
                balances[t.AccountId] += SignedAmountForPrimaryAccount(t);
            }

            if (t.Kind == FinanceTransactionKind.Transfer &&
                t.TransferAccountId.HasValue &&
                balances.ContainsKey(t.TransferAccountId.Value) &&
                IsAfterLatestSnapshot(t.TransferAccountId.Value, t.OccurredOn, latestSnapshots) &&
                !HasSummaryFor(t.TransferAccountId.Value, t.OccurredOn, summaryKeys))
            {
                balances[t.TransferAccountId.Value] += t.Amount;
            }
        }
        foreach (var s in summaries)
        {
            if (balances.ContainsKey(s.AccountId) &&
                IsAfterLatestSnapshot(s.AccountId, s.Month, latestSnapshots))
            {
                balances[s.AccountId] += s.Income - s.Expenses;
            }
        }

        foreach (var account in accounts)
            account.Balance = balances[account.Id];

        await db.SaveChangesAsync();
    }

    public static decimal SignedAmountForPrimaryAccount(FinanceTransaction transaction) =>
        transaction.Kind switch
        {
            FinanceTransactionKind.Income => transaction.Amount,
            FinanceTransactionKind.Dividend => transaction.Amount,
            FinanceTransactionKind.Sell => transaction.Amount,
            FinanceTransactionKind.Expense => -transaction.Amount,
            FinanceTransactionKind.Buy => -transaction.Amount,
            FinanceTransactionKind.Fee => -transaction.Amount,
            FinanceTransactionKind.Transfer => -transaction.Amount,
            _ => 0m
        };

    public static bool IsTradeKind(FinanceTransactionKind kind) => kind is
        FinanceTransactionKind.Buy or
        FinanceTransactionKind.Sell or
        FinanceTransactionKind.Dividend or
        FinanceTransactionKind.Fee;

    public static string? NormalizeSymbol(string? symbol) =>
        string.IsNullOrWhiteSpace(symbol) ? null : symbol.Trim().ToUpperInvariant();

    public static async Task RecalculateHoldings(AppDbContext db, int accountId)
    {
        var trades = await db.FinanceTransactions
            .Where(t => t.AccountId == accountId)
            .Where(t => t.Kind == FinanceTransactionKind.Buy || t.Kind == FinanceTransactionKind.Sell)
            .Where(t => t.Status != FinanceTransactionStatus.Pending)
            .Where(t => t.Symbol != null && t.Symbol != "")
            .OrderBy(t => t.OccurredOn)
            .ThenBy(t => t.Id)
            .ToListAsync();

        var existing = await db.Holdings.Where(h => h.AccountId == accountId).ToListAsync();
        var existingBySymbol = existing.ToDictionary(h => h.Symbol, StringComparer.OrdinalIgnoreCase);

        var seenSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in trades.GroupBy(t => t.Symbol!, StringComparer.OrdinalIgnoreCase))
        {
            var symbol = group.Key;
            seenSymbols.Add(symbol);
            decimal qty = 0m, avgCost = 0m;
            foreach (var t in group)
            {
                var tradeQty = t.Quantity ?? 0m;
                var tradePrice = t.PricePerUnit ?? 0m;
                if (tradeQty <= 0) continue;
                if (t.Kind == FinanceTransactionKind.Buy)
                {
                    var newQty = qty + tradeQty;
                    avgCost = newQty > 0 ? (qty * avgCost + tradeQty * tradePrice) / newQty : 0m;
                    qty = newQty;
                }
                else if (t.Kind == FinanceTransactionKind.Sell)
                {
                    qty -= tradeQty;
                    if (qty <= 0)
                    {
                        qty = 0m;
                        avgCost = 0m;
                    }
                }
            }

            if (existingBySymbol.TryGetValue(symbol, out var holding))
            {
                holding.Quantity = qty;
                holding.AverageCost = avgCost;
                holding.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                db.Holdings.Add(new Holding
                {
                    AccountId = accountId,
                    Symbol = symbol,
                    Quantity = qty,
                    AverageCost = avgCost,
                });
            }
        }

        foreach (var holding in existing)
        {
            if (!seenSymbols.Contains(holding.Symbol))
                db.Holdings.Remove(holding);
        }

        await db.SaveChangesAsync();
    }

    public static bool IsAfterLatestSnapshot(
        int accountId,
        DateTime date,
        Dictionary<int, AccountBalanceSnapshot> latestSnapshots) =>
        !latestSnapshots.TryGetValue(accountId, out var snapshot) || date.Date > snapshot.SnapshotDate.Date;

    public static async Task<Dictionary<int, decimal>> ExpectedClosingBalances(
        AppDbContext db,
        IEnumerable<MonthlyAccountSummary> summaries)
    {
        var result = new Dictionary<int, decimal>();
        foreach (var summary in summaries)
            result[summary.Id] = await ExpectedBalanceAt(db, summary.AccountId, MonthEnd(summary.Month));
        return result;
    }

    public static async Task<decimal> ExpectedBalanceAt(AppDbContext db, int accountId, DateTime date)
    {
        var account = await db.FinanceAccounts.FindAsync(accountId);
        if (account is null) return 0;

        var previousSnapshot = await db.AccountBalanceSnapshots
            .Where(s => s.AccountId == accountId && s.IsReconciled && s.SnapshotDate < date.Date)
            .OrderByDescending(s => s.SnapshotDate)
            .FirstOrDefaultAsync();
        var balance = previousSnapshot?.ActualBalance ?? account.StartingBalance;
        var fromDate = previousSnapshot?.SnapshotDate.Date;
        var summaries = await db.MonthlyAccountSummaries
            .Where(s => s.AccountId == accountId && s.Month <= date.Date)
            .ToListAsync();
        if (fromDate.HasValue)
            summaries = summaries.Where(s => s.Month > fromDate.Value).ToList();
        var summaryKeys = summaries.Select(s => SummaryKey(s.AccountId, s.Month)).ToHashSet();

        var tx = await db.FinanceTransactions
            .Where(t => (t.AccountId == accountId || t.TransferAccountId == accountId) && t.OccurredOn <= date.Date)
            .Where(t => t.Status != FinanceTransactionStatus.Pending)
            .ToListAsync();
        if (fromDate.HasValue)
            tx = tx.Where(t => t.OccurredOn > fromDate.Value).ToList();

        foreach (var t in tx)
        {
            if (t.AccountId == accountId && !HasSummaryFor(accountId, t.OccurredOn, summaryKeys))
                balance += SignedAmountForPrimaryAccount(t);
            if (t.Kind == FinanceTransactionKind.Transfer &&
                t.TransferAccountId == accountId &&
                !HasSummaryFor(accountId, t.OccurredOn, summaryKeys))
                balance += t.Amount;
        }
        foreach (var s in summaries) balance += s.Income - s.Expenses;
        return balance;
    }

    public static async Task<Dictionary<string, decimal>> CategorySpending(AppDbContext db, DateTime month)
    {
        var start = MonthStart(month);
        var end = start.AddMonths(1);
        var summaries = await db.MonthlyAccountSummaries.Where(s => s.Month == start).ToListAsync();
        var summaryKeys = summaries.Select(s => SummaryKey(s.AccountId, s.Month)).ToHashSet();
        var tx = await db.FinanceTransactions
            .Include(t => t.Splits)
            .Where(t => t.Kind == FinanceTransactionKind.Expense && t.OccurredOn >= start && t.OccurredOn < end)
            .Where(t => t.Status != FinanceTransactionStatus.Pending)
            .ToListAsync();
        var result = tx
            .Where(t => !HasSummaryFor(t.AccountId, t.OccurredOn, summaryKeys))
            .SelectMany(ExpandCategoryAmounts)
            .GroupBy(x => x.Category)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var summaryAmount = summaries.Sum(s => s.Expenses);
        if (summaryAmount > 0) result["Bank summaries"] = result.GetValueOrDefault("Bank summaries") + summaryAmount;
        return result;
    }

    /// Yields one (Category, Amount) row per transaction, expanding any TransactionSplits.
    /// Falls back to the transaction's own Category when there are no splits. Used for budget
    /// and statistics aggregation so splits redirect cash to the right category buckets.
    public static IEnumerable<(string Category, decimal Amount)> ExpandCategoryAmounts(FinanceTransaction t)
    {
        if (t.Splits is { Count: > 0 })
        {
            foreach (var split in t.Splits)
            {
                var cat = string.IsNullOrWhiteSpace(split.Category) ? "Uncategorized" : split.Category;
                yield return (cat, split.Amount);
            }
            yield break;
        }
        var fallback = string.IsNullOrWhiteSpace(t.Category) ? "Uncategorized" : t.Category;
        yield return (fallback, t.Amount);
    }

    /// Average month length in days — 365.25/12. Used only for Day/Week intervals
    /// where there's no calendar-aligned "month" to ride; Month/Year intervals
    /// compute their monthly cost directly without this approximation.
    public const decimal DaysPerMonth = 30.4375m;

    /// Average weeks per month — 52/12.
    public const decimal WeeksPerMonth = 52m / 12m;

    /// Computes the per-month cost of a recurring charge given the billing period.
    /// Calendar-aligned units (Month, Year) are exact (no 30-day approximation);
    /// Day/Week use the standard 30.4375-day / 4.333-week month average.
    public static decimal ToMonthlyAmount(decimal amount, BillingIntervalUnit unit, int count)
    {
        var n = Math.Max(1, count);
        return unit switch
        {
            BillingIntervalUnit.Day => Math.Round(amount * DaysPerMonth / n, 2),
            BillingIntervalUnit.Week => Math.Round(amount * WeeksPerMonth / n, 2),
            BillingIntervalUnit.Month => Math.Round(amount / n, 2),
            BillingIntervalUnit.Year => Math.Round(amount / (12m * n), 2),
            _ => amount,
        };
    }

    public static decimal ToMonthlyAmount(Subscription subscription) =>
        ToMonthlyAmount(subscription.Amount, subscription.BillingIntervalUnit, subscription.BillingIntervalCount);

    /// Calendar-aware due-date advance. Month/Year ride the calendar so a 15th-of-month
    /// subscription always lands on the 15th (and Feb 29 lands on Feb 28 in non-leap years);
    /// Day/Week fall back to plain day arithmetic.
    public static DateTime AdvanceDueDate(DateTime currentDueDate, BillingIntervalUnit unit, int count)
    {
        var n = Math.Max(1, count);
        return unit switch
        {
            BillingIntervalUnit.Day => currentDueDate.Date.AddDays(n),
            BillingIntervalUnit.Week => currentDueDate.Date.AddDays(n * 7),
            BillingIntervalUnit.Month => currentDueDate.Date.AddMonths(n),
            BillingIntervalUnit.Year => currentDueDate.Date.AddYears(n),
            _ => currentDueDate.Date.AddDays(n),
        };
    }

    public static DateTime AdvanceDueDate(Subscription subscription) =>
        AdvanceDueDate(subscription.NextDueOn, subscription.BillingIntervalUnit, subscription.BillingIntervalCount);

    /// Day-equivalent of one billing period — used by code that still wants a "step by N days"
    /// approximation (ODS export round-trip, legacy clients). Authoritative billing math uses
    /// Unit + Count directly.
    public static int BillingPeriodInDays(BillingIntervalUnit unit, int count)
    {
        var n = Math.Max(1, count);
        return unit switch
        {
            BillingIntervalUnit.Day => n,
            BillingIntervalUnit.Week => n * 7,
            BillingIntervalUnit.Month => (int)Math.Round(n * (decimal)DaysPerMonth, MidpointRounding.AwayFromZero),
            BillingIntervalUnit.Year => n * 365,
            _ => n,
        };
    }

    public static string SubscriptionTransactionTag(int subscriptionId, DateTime dueDate) =>
        $"subscription:{subscriptionId}:{dueDate:yyyy-MM-dd}";

    public static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static DateTime MonthStart(DateTime value) => new(value.Year, value.Month, 1);

    public static DateTime MonthEnd(DateTime value) => MonthStart(value).AddMonths(1).AddDays(-1);

    public static string SummaryKey(int accountId, DateTime month) =>
        $"{accountId}:{MonthStart(month):yyyy-MM-dd}";

    public static bool HasSummaryFor(int accountId, DateTime date, HashSet<string> summaryKeys) =>
        summaryKeys.Contains(SummaryKey(accountId, date));
}
