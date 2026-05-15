using LuminaVault.Data;
using LuminaVault.Domain;
using Microsoft.EntityFrameworkCore;
using static LuminaVault.Endpoints.FinanceHelpers;
using static LuminaVault.Endpoints.FinanceMappers;

namespace LuminaVault.Endpoints;

/// Aggregate read-only endpoints: dashboard `/summary` and the deeper `/statistics` view.
/// These pull from every other finance entity, so they live separately from the per-resource files.
internal static class FinanceSummaryEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var summary = app.MapGroup("/api/finance").RequireAuthorization().WithTags("Finance");

        summary.MapGet("/summary", async (AppDbContext db) =>
        {
            var now = DateTime.UtcNow.Date;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var nextMonth = monthStart.AddMonths(1);
            var activeAccounts = await db.FinanceAccounts.Where(a => !a.IsArchived).ToListAsync();
            var rawMonthlyTransactions = await db.FinanceTransactions
                .Include(t => t.Account)
                .Where(t => t.OccurredOn >= monthStart && t.OccurredOn < nextMonth)
                .Where(t => t.Status != FinanceTransactionStatus.Pending)
                .ToListAsync();
            var monthlySummaryRows = await db.MonthlyAccountSummaries
                .Include(s => s.Account)
                .Where(s => s.Month == monthStart)
                .ToListAsync();
            var currentSummaryKeys = monthlySummaryRows.Select(s => SummaryKey(s.AccountId, s.Month)).ToHashSet();
            var monthlyTransactions = rawMonthlyTransactions
                .Where(t => !HasSummaryFor(t.AccountId, t.OccurredOn, currentSummaryKeys))
                .ToList();
            var allRecentTransactions = await db.FinanceTransactions
                .Include(t => t.Account)
                .Include(t => t.TransferAccount)
                .OrderByDescending(t => t.OccurredOn)
                .ThenByDescending(t => t.Id)
                .Take(7)
                .ToListAsync();
            var activeSubscriptions = await db.Subscriptions
                .Include(s => s.Account)
                .Where(s => s.Status == SubscriptionStatus.Active)
                .ToListAsync();
            var inventoryValue = await db.Items
                .Select(i => new { i.Value, i.Quantity })
                .ToListAsync();

            var income = monthlyTransactions
                .Where(t => t.Kind == FinanceTransactionKind.Income)
                .Sum(t => t.Amount) + monthlySummaryRows.Sum(s => s.Income);
            var expenses = monthlyTransactions
                .Where(t => t.Kind == FinanceTransactionKind.Expense)
                .Sum(t => t.Amount) + monthlySummaryRows.Sum(s => s.Expenses);
            var cashFlow = income - expenses;
            var recurringMonthly = activeSubscriptions.Sum(s => ToMonthlyAmount(s.Amount, s.BillingIntervalDays));
            var accountNetWorth = activeAccounts.Sum(a => a.Balance);
            var assetValue = inventoryValue.Sum(i => (i.Value ?? 0m) * i.Quantity);
            var holdings = await db.Holdings.Include(h => h.Account).ToListAsync();
            var holdingsMarketValue = holdings.Sum(h =>
                h.LastPrice.HasValue ? h.Quantity * h.LastPrice.Value : h.Quantity * h.AverageCost);
            var holdingsCostBasis = holdings.Sum(h => h.Quantity * h.AverageCost);

            var series = new List<object>();
            for (var i = 5; i >= 0; i--)
            {
                var start = monthStart.AddMonths(-i);
                var end = start.AddMonths(1);
                var tx = await db.FinanceTransactions
                    .Where(t => t.OccurredOn >= start && t.OccurredOn < end)
                    .Where(t => t.Status != FinanceTransactionStatus.Pending)
                    .ToListAsync();
                var summaryRows = await db.MonthlyAccountSummaries
                    .Where(s => s.Month == start)
                    .ToListAsync();
                var summaryKeys = summaryRows.Select(s => SummaryKey(s.AccountId, s.Month)).ToHashSet();
                var unsummarizedTx = tx
                    .Where(t => !HasSummaryFor(t.AccountId, t.OccurredOn, summaryKeys))
                    .ToList();
                var inMonth = unsummarizedTx.Where(t => t.Kind == FinanceTransactionKind.Income).Sum(t => t.Amount)
                    + summaryRows.Sum(s => s.Income);
                var outMonth = unsummarizedTx.Where(t => t.Kind == FinanceTransactionKind.Expense).Sum(t => t.Amount)
                    + summaryRows.Sum(s => s.Expenses);
                series.Add(new
                {
                    month = start.ToString("MMM yyyy"),
                    income = inMonth,
                    expenses = outMonth,
                    net = inMonth - outMonth
                });
            }

            var categories = monthlyTransactions
                .Where(t => t.Kind == FinanceTransactionKind.Expense)
                .GroupBy(t => t.Category)
                .Select(g => new { category = g.Key, amount = g.Sum(t => t.Amount) })
                .Concat(monthlySummaryRows
                    .Where(s => s.Expenses > 0)
                    .GroupBy(_ => "Bank summaries")
                    .Select(g => new { category = g.Key, amount = g.Sum(s => s.Expenses) }))
                .GroupBy(x => x.category)
                .Select(g => new { category = g.Key, amount = g.Sum(x => x.amount) })
                .OrderByDescending(x => x.amount)
                .Take(8)
                .ToList();

            var accountMix = activeAccounts
                .GroupBy(a => a.Type)
                .Select(g => new { type = g.Key.ToString(), balance = g.Sum(a => a.Balance) })
                .OrderByDescending(x => x.balance)
                .ToList();

            var upcoming = activeSubscriptions
                .Where(s => s.NextDueOn >= now && s.NextDueOn <= now.AddDays(30))
                .OrderBy(s => s.NextDueOn)
                .Take(8)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Category,
                    s.NextDueOn,
                    s.Amount,
                    s.Currency,
                    monthlyAmount = ToMonthlyAmount(s.Amount, s.BillingIntervalDays)
                });

            return Results.Ok(new
            {
                netWorth = accountNetWorth + assetValue + holdingsMarketValue,
                accountNetWorth,
                inventoryValue = assetValue,
                holdingsMarketValue,
                holdingsCostBasis,
                holdingsUnrealizedPnL = holdingsMarketValue - holdingsCostBasis,
                monthlyIncome = income,
                monthlyExpenses = expenses,
                monthlyCashFlow = cashFlow,
                savingsRate = income <= 0 ? 0 : Math.Round(cashFlow / income * 100, 1),
                recurringMonthly,
                activeSubscriptionCount = activeSubscriptions.Count,
                accountCount = activeAccounts.Count,
                walletCount = activeAccounts.Count(a =>
                    a.Type is FinanceAccountType.Cash or FinanceAccountType.Crypto),
                investmentValue = activeAccounts
                    .Where(a => a.Type is FinanceAccountType.Investment or FinanceAccountType.Crypto)
                    .Sum(a => a.Balance) + holdingsMarketValue,
                recentTransactions = allRecentTransactions.Select(MapTransaction),
                upcomingSubscriptions = upcoming,
                monthlySeries = series,
                categoryBreakdown = categories,
                accountMix
            });
        });

        summary.MapGet("/statistics", async (AppDbContext db) =>
        {
            var today = DateTime.UtcNow.Date;
            var currentMonth = MonthStart(today);
            var fromMonth = currentMonth.AddMonths(-11);
            var nextMonth = currentMonth.AddMonths(1);

            var accounts = await db.FinanceAccounts
                .Where(a => !a.IsArchived)
                .OrderByDescending(a => a.Balance)
                .ToListAsync();
            var transactions = await db.FinanceTransactions
                .Include(t => t.Account)
                .Include(t => t.TransferAccount)
                .Where(t => t.OccurredOn >= fromMonth && t.OccurredOn < nextMonth)
                .Where(t => t.Status != FinanceTransactionStatus.Pending)
                .ToListAsync();
            var monthlySummaries = await db.MonthlyAccountSummaries
                .Include(s => s.Account)
                .Where(s => s.Month >= fromMonth && s.Month < nextMonth)
                .ToListAsync();
            var summaryKeys = monthlySummaries.Select(s => SummaryKey(s.AccountId, s.Month)).ToHashSet();
            var unsummarizedTransactions = transactions
                .Where(t => !HasSummaryFor(t.AccountId, t.OccurredOn, summaryKeys))
                .ToList();
            var activeSubscriptions = await db.Subscriptions
                .Include(s => s.Account)
                .Where(s => s.Status == SubscriptionStatus.Active)
                .ToListAsync();
            var assets = await db.Items.ToListAsync();

            var assetCategoryBreakdown = assets
                .GroupBy(i => string.IsNullOrWhiteSpace(i.Category) ? "Uncategorized" : i.Category!)
                .Select(g =>
                {
                    var total = g.Sum(i => (i.Value ?? 0m) * i.Quantity);
                    var quantity = g.Sum(i => i.Quantity);
                    return new
                    {
                        category = g.Key,
                        amount = total,
                        count = quantity,
                        average = quantity <= 0 ? 0 : Math.Round(total / quantity, 2)
                    };
                })
                .OrderByDescending(x => x.amount)
                .ToList();

            var subscriptionCategoryBreakdown = activeSubscriptions
                .GroupBy(s => string.IsNullOrWhiteSpace(s.Category) ? "Uncategorized" : s.Category)
                .Select(g =>
                {
                    var monthly = g.Sum(s => ToMonthlyAmount(s.Amount, s.BillingIntervalDays));
                    return new
                    {
                        category = g.Key,
                        monthlyAmount = monthly,
                        annualAmount = Math.Round(monthly * 12, 2),
                        count = g.Count()
                    };
                })
                .OrderByDescending(x => x.monthlyAmount)
                .ToList();

            var transactionExpenseBreakdown = unsummarizedTransactions
                .Where(t => t.Kind == FinanceTransactionKind.Expense)
                .GroupBy(t => string.IsNullOrWhiteSpace(t.Category) ? "Uncategorized" : t.Category)
                .Select(g => new
                {
                    category = g.Key,
                    amount = g.Sum(t => t.Amount),
                    count = g.Count(),
                    average = Math.Round(g.Average(t => t.Amount), 2)
                })
                .Concat(monthlySummaries
                    .Where(s => s.Expenses > 0)
                    .GroupBy(_ => "Bank summaries")
                    .Select(g => new
                    {
                        category = g.Key,
                        amount = g.Sum(s => s.Expenses),
                        count = g.Count(),
                        average = Math.Round(g.Average(s => s.Expenses), 2)
                    }))
                .GroupBy(x => x.category)
                .Select(g => new
                {
                    category = g.Key,
                    amount = g.Sum(x => x.amount),
                    count = g.Sum(x => x.count),
                    average = g.Sum(x => x.count) <= 0 ? 0 : Math.Round(g.Sum(x => x.amount) / g.Sum(x => x.count), 2)
                })
                .OrderByDescending(x => x.amount)
                .ToList();

            var transactionIncomeBreakdown = unsummarizedTransactions
                .Where(t => t.Kind == FinanceTransactionKind.Income)
                .GroupBy(t => string.IsNullOrWhiteSpace(t.Category) ? "Uncategorized" : t.Category)
                .Select(g => new
                {
                    category = g.Key,
                    amount = g.Sum(t => t.Amount),
                    count = g.Count(),
                    average = Math.Round(g.Average(t => t.Amount), 2)
                })
                .Concat(monthlySummaries
                    .Where(s => s.Income > 0)
                    .GroupBy(_ => "Bank summaries")
                    .Select(g => new
                    {
                        category = g.Key,
                        amount = g.Sum(s => s.Income),
                        count = g.Count(),
                        average = Math.Round(g.Average(s => s.Income), 2)
                    }))
                .GroupBy(x => x.category)
                .Select(g => new
                {
                    category = g.Key,
                    amount = g.Sum(x => x.amount),
                    count = g.Sum(x => x.count),
                    average = g.Sum(x => x.count) <= 0 ? 0 : Math.Round(g.Sum(x => x.amount) / g.Sum(x => x.count), 2)
                })
                .OrderByDescending(x => x.amount)
                .ToList();

            var monthlySeries = new List<object>();
            for (var i = 11; i >= 0; i--)
            {
                var start = currentMonth.AddMonths(-i);
                var end = start.AddMonths(1);
                var monthSummaries = monthlySummaries.Where(s => s.Month == start).ToList();
                var monthKeys = monthSummaries.Select(s => SummaryKey(s.AccountId, s.Month)).ToHashSet();
                var monthTransactions = transactions
                    .Where(t => t.OccurredOn >= start && t.OccurredOn < end)
                    .Where(t => !HasSummaryFor(t.AccountId, t.OccurredOn, monthKeys))
                    .ToList();
                var income = monthTransactions.Where(t => t.Kind == FinanceTransactionKind.Income).Sum(t => t.Amount)
                    + monthSummaries.Sum(s => s.Income);
                var expenses = monthTransactions.Where(t => t.Kind == FinanceTransactionKind.Expense).Sum(t => t.Amount)
                    + monthSummaries.Sum(s => s.Expenses);
                monthlySeries.Add(new
                {
                    month = start.ToString("MMM yyyy"),
                    income,
                    expenses,
                    net = income - expenses,
                    summaryCount = monthSummaries.Count,
                    transactionCount = monthTransactions.Count
                });
            }

            var totalAssetValue = assetCategoryBreakdown.Sum(x => x.amount);
            var totalMonthlySubscriptions = subscriptionCategoryBreakdown.Sum(x => x.monthlyAmount);
            var totalTransactionExpenses = transactionExpenseBreakdown.Sum(x => x.amount);
            var totalTransactionIncome = transactionIncomeBreakdown.Sum(x => x.amount);
            var accountNetWorth = accounts.Sum(a => a.Balance);

            return Results.Ok(new
            {
                generatedAt = DateTime.UtcNow,
                rangeStart = fromMonth,
                rangeEnd = currentMonth,
                totals = new
                {
                    netWorth = accountNetWorth + totalAssetValue,
                    accountNetWorth,
                    assetValue = totalAssetValue,
                    monthlySubscriptionCost = totalMonthlySubscriptions,
                    annualSubscriptionCost = Math.Round(totalMonthlySubscriptions * 12, 2),
                    transactionIncome = totalTransactionIncome,
                    transactionExpenses = totalTransactionExpenses,
                    transactionNet = totalTransactionIncome - totalTransactionExpenses,
                    assetCount = assets.Sum(i => i.Quantity),
                    activeSubscriptionCount = activeSubscriptions.Count,
                    transactionCount = unsummarizedTransactions.Count,
                    summarizedMonthCount = monthlySummaries.Count
                },
                assetCategoryBreakdown,
                subscriptionCategoryBreakdown,
                transactionExpenseBreakdown,
                transactionIncomeBreakdown,
                monthlySeries,
                accountBalances = accounts.Select(a => new
                {
                    account = a.Name,
                    type = a.Type.ToString(),
                    balance = a.Balance,
                    currency = a.Currency,
                    color = a.Color
                }),
                topExpenses = unsummarizedTransactions
                    .Where(t => t.Kind == FinanceTransactionKind.Expense)
                    .OrderByDescending(t => t.Amount)
                    .Take(10)
                    .Select(t => new
                    {
                        t.Id,
                        t.Payee,
                        t.Category,
                        t.Amount,
                        t.OccurredOn,
                        accountName = t.Account?.Name
                    }),
                subscriptionRunway = activeSubscriptions
                    .OrderByDescending(s => ToMonthlyAmount(s.Amount, s.BillingIntervalDays))
                    .Take(10)
                    .Select(s => new
                    {
                        s.Id,
                        s.Name,
                        s.Category,
                        s.Amount,
                        s.Currency,
                        monthlyAmount = ToMonthlyAmount(s.Amount, s.BillingIntervalDays),
                        annualAmount = Math.Round(ToMonthlyAmount(s.Amount, s.BillingIntervalDays) * 12, 2),
                        s.NextDueOn
                    })
            });
        });
    }
}
