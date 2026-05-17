using LuminaVault.Data;
using LuminaVault.Domain;
using Microsoft.EntityFrameworkCore;
using static LuminaVault.Endpoints.FinanceAggregations;
using static LuminaVault.Endpoints.FinanceHelpers;
using static LuminaVault.Endpoints.FinanceMappers;

namespace LuminaVault.Endpoints;

/// Aggregate read-only endpoints: dashboard `/summary` and the deeper `/statistics` view.
/// These pull from every other finance entity, so they live separately from the per-resource files.
/// Aggregation math lives in FinanceAggregations; this file is just data loading + DTO shaping.
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
            var seriesStart = monthStart.AddMonths(-5);
            var rates = await CurrencyConversion.LoadRates(db);
            var rateHistory = await CurrencyConversion.LoadRateHistory(db);
            // /summary is a read-only dashboard fetch — AsNoTracking on every load
            // skips the change-tracker entries we'd otherwise build and immediately discard.
            var activeAccounts = await db.FinanceAccounts.AsNoTracking().Where(a => !a.IsArchived).ToListAsync();

            // Pull a 6-month sliding window once so the dashboard's chart series and the
            // current-month totals can both feed off the same query result.
            var windowedTransactions = await db.FinanceTransactions
                .AsNoTracking()
                .Include(t => t.Account)
                .Include(t => t.Splits)
                .Where(t => t.OccurredOn >= seriesStart && t.OccurredOn < nextMonth)
                .Where(t => t.Status != FinanceTransactionStatus.Pending)
                .ToListAsync();
            var windowedSummaries = await db.MonthlyAccountSummaries
                .AsNoTracking()
                .Include(s => s.Account)
                .Where(s => s.Month >= seriesStart && s.Month < nextMonth)
                .ToListAsync();

            var currentMonthSummaries = windowedSummaries.Where(s => s.Month == monthStart).ToList();
            var currentMonthSummaryKeys = currentMonthSummaries.Select(s => SummaryKey(s.AccountId, s.Month)).ToHashSet();
            var currentMonthTransactions = windowedTransactions
                .Where(t => t.OccurredOn >= monthStart && t.OccurredOn < nextMonth)
                .Where(t => !HasSummaryFor(t.AccountId, t.OccurredOn, currentMonthSummaryKeys))
                .ToList();

            var allRecentTransactions = await db.FinanceTransactions
                .AsNoTracking()
                .Include(t => t.Account)
                .Include(t => t.TransferAccount)
                .OrderByDescending(t => t.OccurredOn)
                .ThenByDescending(t => t.Id)
                .Take(7)
                .ToListAsync();
            var activeSubscriptions = await db.Subscriptions
                .AsNoTracking()
                .Include(s => s.Account)
                .Where(s => s.Status == SubscriptionStatus.Active)
                .ToListAsync();
            var inventoryValue = await db.Items
                .AsNoTracking()
                .Select(i => new { i.Value, i.Quantity })
                .ToListAsync();
            var holdings = await db.Holdings.AsNoTracking().Include(h => h.Account).ToListAsync();

            var income = SumKindWithSummaries(currentMonthTransactions, FinanceTransactionKind.Income,
                currentMonthSummaries, s => s.Income, rateHistory);
            var expenses = SumKindWithSummaries(currentMonthTransactions, FinanceTransactionKind.Expense,
                currentMonthSummaries, s => s.Expenses, rateHistory);
            var cashFlow = income - expenses;
            var recurringMonthly = activeSubscriptions.Sum(s =>
                CurrencyConversion.ToBase(ToMonthlyAmount(s), s.Currency, rates));
            var accountNetWorth = activeAccounts.Sum(a => CurrencyConversion.ToBase(a.Balance, a.Currency, rates));
            var assetValue = inventoryValue.Sum(i => (i.Value ?? 0m) * i.Quantity);
            var holdingsMarketValue = holdings.Sum(h =>
                CurrencyConversion.ToBase(
                    h.LastPrice.HasValue ? h.Quantity * h.LastPrice.Value : h.Quantity * h.AverageCost,
                    h.Account?.Currency,
                    rates));
            var holdingsCostBasis = holdings.Sum(h =>
                CurrencyConversion.ToBase(h.Quantity * h.AverageCost, h.Account?.Currency, rates));

            var series = MonthlyIncomeExpenseSeries(monthStart, 6, windowedTransactions, windowedSummaries, rateHistory)
                .Select(p => new
                {
                    month = p.Month.ToString("MMM yyyy"),
                    income = p.Income,
                    expenses = p.Expenses,
                    net = p.Income - p.Expenses,
                });

            var categories = CategoryBreakdown(currentMonthTransactions, FinanceTransactionKind.Expense,
                    currentMonthSummaries, s => s.Expenses, rateHistory)
                .Take(8)
                .Select(c => new { category = c.Category, amount = c.Amount });

            var accountMix = activeAccounts
                .GroupBy(a => a.Type)
                .Select(g => new { type = g.Key.ToString(), balance = g.Sum(a => CurrencyConversion.ToBase(a.Balance, a.Currency, rates)) })
                .OrderByDescending(x => x.balance);

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
                    monthlyAmount = ToMonthlyAmount(s)
                });

            return Results.Ok(new
            {
                netWorth = accountNetWorth + assetValue + holdingsMarketValue,
                baseCurrency = CurrencyConversion.BaseCurrency,
                fxMissingCurrencies = CurrencyConversion.MissingCurrencies(
                    activeAccounts.Select(a => a.Currency)
                        .Concat(activeSubscriptions.Select(s => s.Currency))
                        .Concat(holdings.Select(h => h.Account?.Currency)),
                    rates),
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
                    .Sum(a => CurrencyConversion.ToBase(a.Balance, a.Currency, rates)) + holdingsMarketValue,
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
            var rates = await CurrencyConversion.LoadRates(db);
            var rateHistory = await CurrencyConversion.LoadRateHistory(db);

            // Pure read endpoint — see the matching AsNoTracking pass on /summary above.
            var accounts = await db.FinanceAccounts
                .AsNoTracking()
                .Where(a => !a.IsArchived)
                .OrderByDescending(a => a.Balance)
                .ToListAsync();
            var transactions = await db.FinanceTransactions
                .AsNoTracking()
                .Include(t => t.Account)
                .Include(t => t.TransferAccount)
                .Include(t => t.Splits)
                .Where(t => t.OccurredOn >= fromMonth && t.OccurredOn < nextMonth)
                .Where(t => t.Status != FinanceTransactionStatus.Pending)
                .ToListAsync();
            var monthlySummaries = await db.MonthlyAccountSummaries
                .AsNoTracking()
                .Include(s => s.Account)
                .Where(s => s.Month >= fromMonth && s.Month < nextMonth)
                .ToListAsync();
            var summaryKeys = monthlySummaries.Select(s => SummaryKey(s.AccountId, s.Month)).ToHashSet();
            var unsummarizedTransactions = transactions
                .Where(t => !HasSummaryFor(t.AccountId, t.OccurredOn, summaryKeys))
                .ToList();
            var activeSubscriptions = await db.Subscriptions
                .AsNoTracking()
                .Include(s => s.Account)
                .Where(s => s.Status == SubscriptionStatus.Active)
                .ToListAsync();
            var assets = await db.Items.AsNoTracking().ToListAsync();

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
                    var monthly = g.Sum(s => CurrencyConversion.ToBase(ToMonthlyAmount(s), s.Currency, rates));
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

            var transactionExpenseBreakdown = CategoryStats(unsummarizedTransactions, FinanceTransactionKind.Expense,
                monthlySummaries, s => s.Expenses, rateHistory);
            var transactionIncomeBreakdown = CategoryStats(unsummarizedTransactions, FinanceTransactionKind.Income,
                monthlySummaries, s => s.Income, rateHistory);

            var monthlySeries = MonthlyIncomeExpenseSeries(currentMonth, 12, transactions, monthlySummaries, rateHistory)
                .Select(p =>
                {
                    var monthSummaries = monthlySummaries.Where(s => s.Month == p.Month).ToList();
                    var monthSummaryKeys = monthSummaries.Select(s => SummaryKey(s.AccountId, s.Month)).ToHashSet();
                    var monthTransactions = transactions
                        .Where(t => t.OccurredOn >= p.Month && t.OccurredOn < p.Month.AddMonths(1))
                        .Where(t => !HasSummaryFor(t.AccountId, t.OccurredOn, monthSummaryKeys))
                        .ToList();
                    return new
                    {
                        month = p.Month.ToString("MMM yyyy"),
                        income = p.Income,
                        expenses = p.Expenses,
                        net = p.Income - p.Expenses,
                        summaryCount = monthSummaries.Count,
                        transactionCount = monthTransactions.Count,
                    };
                })
                .ToList();

            var totalAssetValue = assetCategoryBreakdown.Sum(x => x.amount);
            var totalMonthlySubscriptions = subscriptionCategoryBreakdown.Sum(x => x.monthlyAmount);
            var totalTransactionExpenses = transactionExpenseBreakdown.Sum(x => x.Amount);
            var totalTransactionIncome = transactionIncomeBreakdown.Sum(x => x.Amount);
            var accountNetWorth = accounts.Sum(a => CurrencyConversion.ToBase(a.Balance, a.Currency, rates));

            return Results.Ok(new
            {
                generatedAt = DateTime.UtcNow,
                rangeStart = fromMonth,
                rangeEnd = currentMonth,
                baseCurrency = CurrencyConversion.BaseCurrency,
                fxMissingCurrencies = CurrencyConversion.MissingCurrencies(
                    accounts.Select(a => a.Currency)
                        .Concat(activeSubscriptions.Select(s => s.Currency))
                        .Concat(transactions.Select(t => t.Account?.Currency))
                        .Concat(monthlySummaries.Select(s => s.Account?.Currency)),
                    rates),
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
                transactionExpenseBreakdown = transactionExpenseBreakdown.Select(c => new
                {
                    category = c.Category,
                    amount = c.Amount,
                    count = c.Count,
                    average = c.Average,
                }),
                transactionIncomeBreakdown = transactionIncomeBreakdown.Select(c => new
                {
                    category = c.Category,
                    amount = c.Amount,
                    count = c.Count,
                    average = c.Average,
                }),
                monthlySeries,
                accountBalances = accounts.Select(a => new
                {
                    account = a.Name,
                    type = a.Type.ToString(),
                    balance = a.Balance,
                    baseBalance = CurrencyConversion.ToBase(a.Balance, a.Currency, rates),
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
                    .OrderByDescending(s => CurrencyConversion.ToBase(ToMonthlyAmount(s), s.Currency, rates))
                    .Take(10)
                    .Select(s => new
                    {
                        s.Id,
                        s.Name,
                        s.Category,
                        s.Amount,
                        s.Currency,
                        monthlyAmount = ToMonthlyAmount(s),
                        monthlyAmountBase = CurrencyConversion.ToBase(ToMonthlyAmount(s), s.Currency, rates),
                        annualAmount = Math.Round(ToMonthlyAmount(s) * 12, 2),
                        s.NextDueOn
                    })
            });
        });
    }
}
