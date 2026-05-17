using LuminaVault.Domain;
using static LuminaVault.Endpoints.FinanceHelpers;

namespace LuminaVault.Endpoints;

/// Shared aggregation primitives for the dashboard `/summary` and `/statistics` endpoints.
///
/// Both endpoints repeat the same patterns: "sum income for these transactions plus these
/// monthly summaries, all in base currency"; "expand transactions through splits, group by
/// category, optionally add a Bank-summaries bucket"; "walk N months and produce {income,
/// expenses, net} per month."
///
/// Pulling those into named methods cuts duplication, makes the math easier to scan, and
/// gives the unit tests a way to exercise it without going through the full endpoint.
internal static class FinanceAggregations
{
    public record CategoryAmount(string Category, decimal Amount);
    public record CategoryStat(string Category, decimal Amount, int Count, decimal Average);
    public record MonthIncomeExpense(DateTime Month, decimal Income, decimal Expenses);

    /// Sum a kind of transaction in base currency, plus the corresponding leg of any
    /// MonthlyAccountSummaries (which already aggregate cleared transactions for closed months).
    /// `summaryAmount` picks Income vs Expenses out of the summary row.
    public static decimal SumKindWithSummaries(
        IEnumerable<FinanceTransaction> unsummarizedTransactions,
        FinanceTransactionKind kind,
        IEnumerable<MonthlyAccountSummary> summaries,
        Func<MonthlyAccountSummary, decimal> summaryAmount,
        IReadOnlyDictionary<string, List<ExchangeRatePoint>> rateHistory)
    {
        var txTotal = unsummarizedTransactions
            .Where(t => t.Kind == kind)
            .Sum(t => CurrencyConversion.ToBase(t.Amount, t.Account?.Currency, t.OccurredOn, rateHistory));
        var summaryTotal = summaries.Sum(s =>
            CurrencyConversion.ToBase(summaryAmount(s), s.Account?.Currency, s.Month, rateHistory));
        return txTotal + summaryTotal;
    }

    /// Category-bucketed totals for one kind: walks splits via ExpandCategoryAmounts, optionally
    /// folds the summary-row totals into a synthetic "Bank summaries" bucket so the user can see
    /// how much closed-month aggregate spending contributed without losing it in the breakdown.
    public static List<CategoryAmount> CategoryBreakdown(
        IEnumerable<FinanceTransaction> unsummarizedTransactions,
        FinanceTransactionKind kind,
        IEnumerable<MonthlyAccountSummary> summaries,
        Func<MonthlyAccountSummary, decimal> summaryAmount,
        IReadOnlyDictionary<string, List<ExchangeRatePoint>> rateHistory)
    {
        var fromTransactions = unsummarizedTransactions
            .Where(t => t.Kind == kind)
            .SelectMany(t => ExpandCategoryAmounts(t).Select(x => new
            {
                category = x.Category,
                amount = CurrencyConversion.ToBase(x.Amount, t.Account?.Currency, t.OccurredOn, rateHistory)
            }));
        var fromSummaries = summaries
            .Where(s => summaryAmount(s) > 0)
            .Select(s => new
            {
                category = "Bank summaries",
                amount = CurrencyConversion.ToBase(summaryAmount(s), s.Account?.Currency, s.Month, rateHistory)
            });
        return fromTransactions.Concat(fromSummaries)
            .GroupBy(x => x.category)
            .Select(g => new CategoryAmount(g.Key, g.Sum(x => x.amount)))
            .OrderByDescending(x => x.Amount)
            .ToList();
    }

    /// Same as CategoryBreakdown but additionally tracks per-bucket count and average — the
    /// statistics endpoint wants both. Kept distinct from CategoryBreakdown so dashboard
    /// callers don't pay the extra GroupBy step.
    public static List<CategoryStat> CategoryStats(
        IEnumerable<FinanceTransaction> unsummarizedTransactions,
        FinanceTransactionKind kind,
        IEnumerable<MonthlyAccountSummary> summaries,
        Func<MonthlyAccountSummary, decimal> summaryAmount,
        IReadOnlyDictionary<string, List<ExchangeRatePoint>> rateHistory)
    {
        var fromTransactions = unsummarizedTransactions
            .Where(t => t.Kind == kind)
            .SelectMany(t => ExpandCategoryAmounts(t).Select(x => new
            {
                category = x.Category,
                amount = CurrencyConversion.ToBase(x.Amount, t.Account?.Currency, t.OccurredOn, rateHistory)
            }))
            .GroupBy(x => x.category)
            .Select(g => new
            {
                category = g.Key,
                amount = g.Sum(x => x.amount),
                count = g.Count(),
                average = Math.Round(g.Average(x => x.amount), 2)
            });
        var fromSummaries = summaries
            .Where(s => summaryAmount(s) > 0)
            .GroupBy(_ => "Bank summaries")
            .Select(g => new
            {
                category = g.Key,
                amount = g.Sum(s => CurrencyConversion.ToBase(summaryAmount(s), s.Account?.Currency, s.Month, rateHistory)),
                count = g.Count(),
                average = Math.Round(g.Average(s => CurrencyConversion.ToBase(summaryAmount(s), s.Account?.Currency, s.Month, rateHistory)), 2)
            });
        return fromTransactions.Concat(fromSummaries)
            .GroupBy(x => x.category)
            .Select(g =>
            {
                var totalAmount = g.Sum(x => x.amount);
                var totalCount = g.Sum(x => x.count);
                return new CategoryStat(
                    g.Key,
                    totalAmount,
                    totalCount,
                    totalCount <= 0 ? 0 : Math.Round(totalAmount / totalCount, 2));
            })
            .OrderByDescending(x => x.Amount)
            .ToList();
    }

    /// Walk N months ending at (and including) `endMonth`, yielding income/expenses in base
    /// currency for each. The caller hands in all transactions + summaries across the window
    /// once (already loaded) so this stays in-memory; we filter per-month inside.
    public static List<MonthIncomeExpense> MonthlyIncomeExpenseSeries(
        DateTime endMonth,
        int months,
        IEnumerable<FinanceTransaction> transactionsInRange,
        IEnumerable<MonthlyAccountSummary> summariesInRange,
        IReadOnlyDictionary<string, List<ExchangeRatePoint>> rateHistory)
    {
        var transactionList = transactionsInRange.ToList();
        var summaryList = summariesInRange.ToList();
        var result = new List<MonthIncomeExpense>(months);
        for (var i = months - 1; i >= 0; i--)
        {
            var start = endMonth.AddMonths(-i);
            var end = start.AddMonths(1);
            var monthSummaries = summaryList.Where(s => s.Month == start).ToList();
            var monthKeys = monthSummaries.Select(s => SummaryKey(s.AccountId, s.Month)).ToHashSet();
            var monthTransactions = transactionList
                .Where(t => t.OccurredOn >= start && t.OccurredOn < end)
                .Where(t => !HasSummaryFor(t.AccountId, t.OccurredOn, monthKeys))
                .ToList();
            var income = SumKindWithSummaries(monthTransactions, FinanceTransactionKind.Income,
                monthSummaries, s => s.Income, rateHistory);
            var expenses = SumKindWithSummaries(monthTransactions, FinanceTransactionKind.Expense,
                monthSummaries, s => s.Expenses, rateHistory);
            result.Add(new MonthIncomeExpense(start, income, expenses));
        }
        return result;
    }
}
