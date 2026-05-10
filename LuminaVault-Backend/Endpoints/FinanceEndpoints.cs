using LuminaVault.Data;
using LuminaVault.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

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
    string[] Tags, DateTime CreatedAt, DateTime UpdatedAt);

public record FinanceTransactionInput(
    int AccountId, int? TransferAccountId, FinanceTransactionKind Kind, FinanceTransactionStatus Status,
    DateTime OccurredOn, string Payee, string Category, decimal Amount,
    string? Description, string? Notes, string[] Tags);

public record MonthlyAccountSummaryDto(
    int Id, int AccountId, string? AccountName, string Currency, DateTime Month,
    decimal Income, decimal Expenses, decimal Net, decimal? OpeningBalance,
    decimal? ClosingBalance, string? Notes, DateTime CreatedAt, DateTime UpdatedAt);

public record MonthlyAccountSummaryInput(
    int AccountId, DateTime Month, decimal Income, decimal Expenses,
    decimal? OpeningBalance, decimal? ClosingBalance, string? Notes);

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

public static class FinanceEndpoints
{
    public static IEndpointRouteBuilder MapFinance(this IEndpointRouteBuilder app)
    {
        var accounts = app.MapGroup("/api/finance/accounts").RequireAuthorization().WithTags("Finance");
        var transactions = app.MapGroup("/api/finance/transactions").RequireAuthorization().WithTags("Finance");
        var monthlySummaries = app.MapGroup("/api/finance/monthly-summaries").RequireAuthorization().WithTags("Finance");
        var subscriptions = app.MapGroup("/api/finance/subscriptions").RequireAuthorization().WithTags("Finance");
        var budgets = app.MapGroup("/api/finance/budgets").RequireAuthorization().WithTags("Finance");
        var balanceSnapshots = app.MapGroup("/api/finance/balance-snapshots").RequireAuthorization().WithTags("Finance");
        var summary = app.MapGroup("/api/finance").RequireAuthorization().WithTags("Finance");

        accounts.MapGet("/", async (AppDbContext db, bool includeArchived = false) =>
        {
            await RecalculateBalances(db);
            var query = db.FinanceAccounts.AsQueryable();
            if (!includeArchived) query = query.Where(a => !a.IsArchived);
            var result = await query.OrderBy(a => a.IsArchived).ThenBy(a => a.Name).ToListAsync();
            return Results.Ok(result.Select(MapAccount));
        });

        accounts.MapPost("/", async ([FromBody] FinanceAccountInput input, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(input.Name))
                return Results.BadRequest(new { error = "Account name is required." });

            var account = new FinanceAccount();
            ApplyAccount(account, input);
            account.Balance = input.Balance;
            account.StartingBalance = input.StartingBalance == 0 ? input.Balance : input.StartingBalance;
            db.FinanceAccounts.Add(account);
            await db.SaveChangesAsync();
            return Results.Created($"/api/finance/accounts/{account.Id}", MapAccount(account));
        });

        accounts.MapPut("/{id:int}", async (int id, [FromBody] FinanceAccountInput input, AppDbContext db) =>
        {
            var account = await db.FinanceAccounts.FindAsync(id);
            if (account is null) return Results.NotFound();
            ApplyAccount(account, input);
            await db.SaveChangesAsync();
            await RecalculateBalances(db);
            await db.Entry(account).ReloadAsync();
            return Results.Ok(MapAccount(account));
        });

        accounts.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var account = await db.FinanceAccounts.FindAsync(id);
            if (account is null) return Results.NotFound();
            var hasActivity = await db.FinanceTransactions.AnyAsync(t =>
                t.AccountId == id || t.TransferAccountId == id);
            if (hasActivity)
            {
                account.IsArchived = true;
            }
            else
            {
                db.FinanceAccounts.Remove(account);
            }
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        transactions.MapGet("/", async (
            AppDbContext db, string? q, int? accountId, string? category,
            FinanceTransactionKind? kind, DateTime? from, DateTime? to) =>
        {
            var query = db.FinanceTransactions
                .Include(t => t.Account)
                .Include(t => t.TransferAccount)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var needle = q.Trim().ToLower();
                query = query.Where(t =>
                    t.Payee.ToLower().Contains(needle) ||
                    t.Category.ToLower().Contains(needle) ||
                    (t.Description ?? "").ToLower().Contains(needle) ||
                    (t.Notes ?? "").ToLower().Contains(needle) ||
                    t.TagsCsv.ToLower().Contains(needle));
            }
            if (accountId.HasValue)
                query = query.Where(t => t.AccountId == accountId || t.TransferAccountId == accountId);
            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(t => t.Category == category);
            if (kind.HasValue)
                query = query.Where(t => t.Kind == kind);
            if (from.HasValue)
                query = query.Where(t => t.OccurredOn >= from.Value.Date);
            if (to.HasValue)
                query = query.Where(t => t.OccurredOn <= to.Value.Date);

            var result = await query
                .OrderByDescending(t => t.OccurredOn)
                .ThenByDescending(t => t.Id)
                .Take(800)
                .ToListAsync();
            return Results.Ok(result.Select(MapTransaction));
        });

        transactions.MapPost("/", async ([FromBody] FinanceTransactionInput input, AppDbContext db) =>
        {
            var validation = await ValidateTransaction(input, db);
            if (validation is not null) return validation;

            var transaction = new FinanceTransaction();
            ApplyTransaction(transaction, input);
            db.FinanceTransactions.Add(transaction);
            await db.SaveChangesAsync();
            await RecalculateBalances(db);
            await LoadTransactionRefs(db, transaction);
            return Results.Created($"/api/finance/transactions/{transaction.Id}", MapTransaction(transaction));
        });

        transactions.MapPut("/{id:int}", async (int id, [FromBody] FinanceTransactionInput input, AppDbContext db) =>
        {
            var transaction = await db.FinanceTransactions
                .Include(t => t.Account)
                .Include(t => t.TransferAccount)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (transaction is null) return Results.NotFound();
            var validation = await ValidateTransaction(input, db);
            if (validation is not null) return validation;

            ApplyTransaction(transaction, input);
            transaction.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await RecalculateBalances(db);
            await LoadTransactionRefs(db, transaction);
            return Results.Ok(MapTransaction(transaction));
        });

        transactions.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var transaction = await db.FinanceTransactions.FindAsync(id);
            if (transaction is null) return Results.NotFound();
            db.FinanceTransactions.Remove(transaction);
            await db.SaveChangesAsync();
            await RecalculateBalances(db);
            return Results.NoContent();
        });

        monthlySummaries.MapGet("/", async (AppDbContext db, int? accountId, DateTime? from, DateTime? to) =>
        {
            var query = db.MonthlyAccountSummaries.Include(s => s.Account).AsQueryable();
            if (accountId.HasValue) query = query.Where(s => s.AccountId == accountId.Value);
            if (from.HasValue) query = query.Where(s => s.Month >= MonthStart(from.Value));
            if (to.HasValue) query = query.Where(s => s.Month <= MonthStart(to.Value));

            var result = await query
                .OrderByDescending(s => s.Month)
                .ThenBy(s => s.Account!.Name)
                .Take(500)
                .ToListAsync();
            return Results.Ok(result.Select(MapMonthlySummary));
        });

        monthlySummaries.MapPost("/", async ([FromBody] MonthlyAccountSummaryInput input, AppDbContext db) =>
        {
            var validation = await ValidateMonthlySummary(input, db);
            if (validation is not null) return validation;

            var month = MonthStart(input.Month);
            if (await db.MonthlyAccountSummaries.AnyAsync(s => s.AccountId == input.AccountId && s.Month == month))
                return Results.Conflict(new { error = "This account already has a summary for that month." });

            var monthlySummary = new MonthlyAccountSummary();
            ApplyMonthlySummary(monthlySummary, input);
            db.MonthlyAccountSummaries.Add(monthlySummary);
            await db.SaveChangesAsync();
            await RecalculateBalances(db);
            await db.Entry(monthlySummary).Reference(s => s.Account).LoadAsync();
            return Results.Created($"/api/finance/monthly-summaries/{monthlySummary.Id}", MapMonthlySummary(monthlySummary));
        });

        monthlySummaries.MapPut("/{id:int}", async (int id, [FromBody] MonthlyAccountSummaryInput input, AppDbContext db) =>
        {
            var monthlySummary = await db.MonthlyAccountSummaries
                .Include(s => s.Account)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (monthlySummary is null) return Results.NotFound();
            var validation = await ValidateMonthlySummary(input, db);
            if (validation is not null) return validation;

            var month = MonthStart(input.Month);
            if (await db.MonthlyAccountSummaries.AnyAsync(s =>
                s.Id != id && s.AccountId == input.AccountId && s.Month == month))
            {
                return Results.Conflict(new { error = "This account already has a summary for that month." });
            }

            ApplyMonthlySummary(monthlySummary, input);
            monthlySummary.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await RecalculateBalances(db);
            await db.Entry(monthlySummary).Reference(s => s.Account).LoadAsync();
            return Results.Ok(MapMonthlySummary(monthlySummary));
        });

        monthlySummaries.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var monthlySummary = await db.MonthlyAccountSummaries.FindAsync(id);
            if (monthlySummary is null) return Results.NotFound();
            db.MonthlyAccountSummaries.Remove(monthlySummary);
            await db.SaveChangesAsync();
            await RecalculateBalances(db);
            return Results.NoContent();
        });

        subscriptions.MapGet("/", async (AppDbContext db, bool includeInactive = false) =>
        {
            var query = db.Subscriptions.Include(s => s.Account).Include(s => s.Attachments).AsQueryable();
            if (!includeInactive) query = query.Where(s => s.Status != SubscriptionStatus.Cancelled);
            var result = await query
                .OrderBy(s => s.Status == SubscriptionStatus.Active ? 0 : 1)
                .ThenBy(s => s.NextDueOn)
                .ToListAsync();
            return Results.Ok(result.Select(MapSubscription));
        });

        subscriptions.MapPost("/", async ([FromBody] SubscriptionInput input, AppDbContext db) =>
        {
            var validation = await ValidateSubscription(input, db);
            if (validation is not null) return validation;
            var subscription = new Subscription();
            ApplySubscription(subscription, input);
            db.Subscriptions.Add(subscription);
            await db.SaveChangesAsync();
            await db.Entry(subscription).Reference(s => s.Account).LoadAsync();
            return Results.Created($"/api/finance/subscriptions/{subscription.Id}", MapSubscription(subscription));
        });

        subscriptions.MapPut("/{id:int}", async (int id, [FromBody] SubscriptionInput input, AppDbContext db) =>
        {
            var subscription = await db.Subscriptions.Include(s => s.Account).Include(s => s.Attachments).FirstOrDefaultAsync(s => s.Id == id);
            if (subscription is null) return Results.NotFound();
            var validation = await ValidateSubscription(input, db);
            if (validation is not null) return validation;
            ApplySubscription(subscription, input);
            subscription.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await db.Entry(subscription).Reference(s => s.Account).LoadAsync();
            return Results.Ok(MapSubscription(subscription));
        });

        subscriptions.MapDelete("/{id:int}", async (int id, AppDbContext db, IWebHostEnvironment env) =>
        {
            var subscription = await db.Subscriptions.Include(s => s.Attachments).FirstOrDefaultAsync(s => s.Id == id);
            if (subscription is null) return Results.NotFound();
            foreach (var attachment in subscription.Attachments)
                AttachmentEndpoints.DeleteUploadFile(env, attachment.FileName);
            db.Subscriptions.Remove(subscription);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        budgets.MapGet("/", async (AppDbContext db, DateTime? month) =>
        {
            var targetMonth = MonthStart(month ?? DateTime.UtcNow);
            var spent = await CategorySpending(db, targetMonth);
            var result = await db.FinanceBudgets
                .Where(b => b.Month == targetMonth)
                .OrderBy(b => b.Category)
                .ToListAsync();
            return Results.Ok(result.Select(b => MapBudget(b, spent.GetValueOrDefault(b.Category, 0m))));
        });

        budgets.MapGet("/overview", async (AppDbContext db, DateTime? month) =>
        {
            var targetMonth = MonthStart(month ?? DateTime.UtcNow);
            var spent = await CategorySpending(db, targetMonth);
            var budgetsForMonth = await db.FinanceBudgets
                .Where(b => b.Month == targetMonth)
                .OrderBy(b => b.Category)
                .ToListAsync();
            var rows = budgetsForMonth.Select(b => MapBudget(b, spent.GetValueOrDefault(b.Category, 0m))).ToList();
            return Results.Ok(new
            {
                month = targetMonth,
                totalBudget = rows.Sum(r => r.LimitAmount),
                totalSpent = rows.Sum(r => r.Spent),
                remaining = rows.Sum(r => r.Remaining),
                rows
            });
        });

        budgets.MapPost("/", async ([FromBody] FinanceBudgetInput input, AppDbContext db) =>
        {
            var validation = ValidateBudget(input);
            if (validation is not null) return validation;
            var month = MonthStart(input.Month);
            if (await db.FinanceBudgets.AnyAsync(b => b.Category == input.Category.Trim() && b.Month == month))
                return Results.Conflict(new { error = "This category already has a budget for that month." });
            var budget = new FinanceBudget();
            ApplyBudget(budget, input);
            db.FinanceBudgets.Add(budget);
            await db.SaveChangesAsync();
            var spent = await CategorySpending(db, budget.Month);
            return Results.Created($"/api/finance/budgets/{budget.Id}", MapBudget(budget, spent.GetValueOrDefault(budget.Category, 0m)));
        });

        budgets.MapPut("/{id:int}", async (int id, [FromBody] FinanceBudgetInput input, AppDbContext db) =>
        {
            var budget = await db.FinanceBudgets.FindAsync(id);
            if (budget is null) return Results.NotFound();
            var validation = ValidateBudget(input);
            if (validation is not null) return validation;
            var category = input.Category.Trim();
            var month = MonthStart(input.Month);
            if (await db.FinanceBudgets.AnyAsync(b => b.Id != id && b.Category == category && b.Month == month))
                return Results.Conflict(new { error = "This category already has a budget for that month." });
            ApplyBudget(budget, input);
            budget.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            var spent = await CategorySpending(db, budget.Month);
            return Results.Ok(MapBudget(budget, spent.GetValueOrDefault(budget.Category, 0m)));
        });

        budgets.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var budget = await db.FinanceBudgets.FindAsync(id);
            if (budget is null) return Results.NotFound();
            db.FinanceBudgets.Remove(budget);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        balanceSnapshots.MapGet("/", async (AppDbContext db, int? accountId) =>
        {
            await RecalculateBalances(db);
            var query = db.AccountBalanceSnapshots.Include(s => s.Account).AsQueryable();
            if (accountId.HasValue) query = query.Where(s => s.AccountId == accountId.Value);
            var result = await query
                .OrderByDescending(s => s.SnapshotDate)
                .ThenBy(s => s.Account!.Name)
                .Take(200)
                .ToListAsync();
            return Results.Ok(result.Select(MapBalanceSnapshot));
        });

        balanceSnapshots.MapPost("/", async ([FromBody] AccountBalanceSnapshotInput input, AppDbContext db) =>
        {
            var validation = await ValidateBalanceSnapshot(input, db);
            if (validation is not null) return validation;
            var date = input.SnapshotDate.Date;
            if (await db.AccountBalanceSnapshots.AnyAsync(s => s.AccountId == input.AccountId && s.SnapshotDate == date))
                return Results.Conflict(new { error = "This account already has a snapshot for that date." });
            var expected = await ExpectedBalanceAt(db, input.AccountId, date);
            var snapshot = new AccountBalanceSnapshot();
            ApplyBalanceSnapshot(snapshot, input, expected);
            db.AccountBalanceSnapshots.Add(snapshot);
            await db.SaveChangesAsync();
            await RecalculateBalances(db);
            await db.Entry(snapshot).Reference(s => s.Account).LoadAsync();
            return Results.Created($"/api/finance/balance-snapshots/{snapshot.Id}", MapBalanceSnapshot(snapshot));
        });

        balanceSnapshots.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var snapshot = await db.AccountBalanceSnapshots.FindAsync(id);
            if (snapshot is null) return Results.NotFound();
            db.AccountBalanceSnapshots.Remove(snapshot);
            await db.SaveChangesAsync();
            await RecalculateBalances(db);
            return Results.NoContent();
        });

        summary.MapGet("/summary", async (AppDbContext db) =>
        {
            await RecalculateBalances(db);

            var now = DateTime.UtcNow.Date;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var nextMonth = monthStart.AddMonths(1);
            var activeAccounts = await db.FinanceAccounts.Where(a => !a.IsArchived).ToListAsync();
            var rawMonthlyTransactions = await db.FinanceTransactions
                .Include(t => t.Account)
                .Where(t => t.OccurredOn >= monthStart && t.OccurredOn < nextMonth)
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

            var series = new List<object>();
            for (var i = 5; i >= 0; i--)
            {
                var start = monthStart.AddMonths(-i);
                var end = start.AddMonths(1);
                var tx = await db.FinanceTransactions
                    .Where(t => t.OccurredOn >= start && t.OccurredOn < end)
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
                netWorth = accountNetWorth + assetValue,
                accountNetWorth,
                inventoryValue = assetValue,
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
                    .Sum(a => a.Balance),
                recentTransactions = allRecentTransactions.Select(MapTransaction),
                upcomingSubscriptions = upcoming,
                monthlySeries = series,
                categoryBreakdown = categories,
                accountMix
            });
        });

        summary.MapGet("/statistics", async (AppDbContext db) =>
        {
            await RecalculateBalances(db);

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

        return app;
    }

    static void ApplyAccount(FinanceAccount account, FinanceAccountInput input)
    {
        account.Name = input.Name.Trim();
        account.Institution = Clean(input.Institution);
        account.Type = input.Type;
        account.Currency = Clean(input.Currency)?.ToUpperInvariant() ?? "CHF";
        account.StartingBalance = input.StartingBalance;
        account.Color = string.IsNullOrWhiteSpace(input.Color) ? "#14b8a6" : input.Color.Trim();
        account.Notes = Clean(input.Notes);
        account.IsArchived = input.IsArchived;
    }

    static void ApplyTransaction(FinanceTransaction transaction, FinanceTransactionInput input)
    {
        transaction.AccountId = input.AccountId;
        transaction.TransferAccountId = input.Kind == FinanceTransactionKind.Transfer
            ? input.TransferAccountId
            : null;
        transaction.Kind = input.Kind;
        transaction.Status = input.Status;
        transaction.OccurredOn = input.OccurredOn.Date;
        transaction.Payee = input.Payee.Trim();
        transaction.Category = string.IsNullOrWhiteSpace(input.Category) ? "General" : input.Category.Trim();
        transaction.Amount = Math.Abs(input.Amount);
        transaction.Description = Clean(input.Description);
        transaction.Notes = Clean(input.Notes);
        transaction.TagsCsv = string.Join(",", (input.Tags ?? Array.Empty<string>())
            .Select(t => t.Trim()).Where(t => t.Length > 0));
    }

    static void ApplyMonthlySummary(MonthlyAccountSummary monthlySummary, MonthlyAccountSummaryInput input)
    {
        monthlySummary.AccountId = input.AccountId;
        monthlySummary.Month = MonthStart(input.Month);
        monthlySummary.Income = Math.Abs(input.Income);
        monthlySummary.Expenses = Math.Abs(input.Expenses);
        monthlySummary.OpeningBalance = input.OpeningBalance;
        monthlySummary.ClosingBalance = input.ClosingBalance;
        monthlySummary.Notes = Clean(input.Notes);
    }

    static void ApplySubscription(Subscription subscription, SubscriptionInput input)
    {
        subscription.Name = input.Name.Trim();
        subscription.Category = string.IsNullOrWhiteSpace(input.Category) ? "Subscriptions" : input.Category.Trim();
        subscription.Provider = Clean(input.Provider);
        subscription.AccountId = input.AccountId;
        subscription.Amount = Math.Abs(input.Amount);
        subscription.Currency = Clean(input.Currency)?.ToUpperInvariant() ?? "CHF";
        subscription.BillingIntervalDays = Math.Max(1, input.BillingIntervalDays);
        subscription.StartedOn = input.StartedOn.Date;
        subscription.NextDueOn = input.NextDueOn.Date;
        subscription.AutoRenew = input.AutoRenew;
        subscription.Status = input.Status;
        subscription.Notes = Clean(input.Notes);
    }

    static void ApplyBudget(FinanceBudget budget, FinanceBudgetInput input)
    {
        budget.Category = input.Category.Trim();
        budget.Month = MonthStart(input.Month);
        budget.LimitAmount = Math.Abs(input.LimitAmount);
        budget.Notes = Clean(input.Notes);
    }

    static void ApplyBalanceSnapshot(
        AccountBalanceSnapshot snapshot,
        AccountBalanceSnapshotInput input,
        decimal expected)
    {
        snapshot.AccountId = input.AccountId;
        snapshot.SnapshotDate = input.SnapshotDate.Date;
        snapshot.ActualBalance = input.ActualBalance;
        snapshot.ExpectedBalance = expected;
        snapshot.Difference = input.ActualBalance - expected;
        snapshot.IsReconciled = input.IsReconciled;
        snapshot.Notes = Clean(input.Notes);
    }

    static async Task<IResult?> ValidateTransaction(FinanceTransactionInput input, AppDbContext db)
    {
        if (input.AccountId <= 0 || !await db.FinanceAccounts.AnyAsync(a => a.Id == input.AccountId))
            return Results.BadRequest(new { error = "Choose a valid account." });
        if (input.Kind == FinanceTransactionKind.Transfer)
        {
            if (input.TransferAccountId is null || input.TransferAccountId == input.AccountId)
                return Results.BadRequest(new { error = "Choose a different destination account." });
            if (!await db.FinanceAccounts.AnyAsync(a => a.Id == input.TransferAccountId.Value))
                return Results.BadRequest(new { error = "Choose a valid destination account." });
        }
        if (string.IsNullOrWhiteSpace(input.Payee))
            return Results.BadRequest(new { error = "Payee is required." });
        if (input.Amount <= 0)
            return Results.BadRequest(new { error = "Amount must be greater than zero." });
        return null;
    }

    static async Task<IResult?> ValidateMonthlySummary(MonthlyAccountSummaryInput input, AppDbContext db)
    {
        if (input.AccountId <= 0 || !await db.FinanceAccounts.AnyAsync(a => a.Id == input.AccountId))
            return Results.BadRequest(new { error = "Choose a valid account." });
        if (input.Income < 0 || input.Expenses < 0)
            return Results.BadRequest(new { error = "Income and expenses cannot be negative." });
        return null;
    }

    static async Task<IResult?> ValidateSubscription(SubscriptionInput input, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            return Results.BadRequest(new { error = "Subscription name is required." });
        if (input.Amount <= 0)
            return Results.BadRequest(new { error = "Amount must be greater than zero." });
        if (input.AccountId.HasValue && !await db.FinanceAccounts.AnyAsync(a => a.Id == input.AccountId.Value))
            return Results.BadRequest(new { error = "Choose a valid account." });
        return null;
    }

    static IResult? ValidateBudget(FinanceBudgetInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Category))
            return Results.BadRequest(new { error = "Choose a category." });
        if (input.LimitAmount <= 0)
            return Results.BadRequest(new { error = "Budget amount must be greater than zero." });
        return null;
    }

    static async Task<IResult?> ValidateBalanceSnapshot(AccountBalanceSnapshotInput input, AppDbContext db)
    {
        if (input.AccountId <= 0 || !await db.FinanceAccounts.AnyAsync(a => a.Id == input.AccountId))
            return Results.BadRequest(new { error = "Choose a valid account." });
        return null;
    }

    static async Task LoadTransactionRefs(AppDbContext db, FinanceTransaction transaction)
    {
        await db.Entry(transaction).Reference(t => t.Account).LoadAsync();
        await db.Entry(transaction).Reference(t => t.TransferAccount).LoadAsync();
    }

    static async Task RecalculateBalances(AppDbContext db)
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
        var tx = await db.FinanceTransactions.ToListAsync();
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

    static decimal SignedAmountForPrimaryAccount(FinanceTransaction transaction) =>
        transaction.Kind switch
        {
            FinanceTransactionKind.Income => transaction.Amount,
            FinanceTransactionKind.Expense => -transaction.Amount,
            FinanceTransactionKind.Transfer => -transaction.Amount,
            _ => 0m
        };

    static bool IsAfterLatestSnapshot(
        int accountId,
        DateTime date,
        Dictionary<int, AccountBalanceSnapshot> latestSnapshots) =>
        !latestSnapshots.TryGetValue(accountId, out var snapshot) || date.Date > snapshot.SnapshotDate.Date;

    static async Task<decimal> ExpectedBalanceAt(AppDbContext db, int accountId, DateTime date)
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

    static async Task<Dictionary<string, decimal>> CategorySpending(AppDbContext db, DateTime month)
    {
        var start = MonthStart(month);
        var end = start.AddMonths(1);
        var summaries = await db.MonthlyAccountSummaries.Where(s => s.Month == start).ToListAsync();
        var summaryKeys = summaries.Select(s => SummaryKey(s.AccountId, s.Month)).ToHashSet();
        var tx = await db.FinanceTransactions
            .Where(t => t.Kind == FinanceTransactionKind.Expense && t.OccurredOn >= start && t.OccurredOn < end)
            .ToListAsync();
        var result = tx
            .Where(t => !HasSummaryFor(t.AccountId, t.OccurredOn, summaryKeys))
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Category) ? "Uncategorized" : t.Category)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
        var summaryAmount = summaries.Sum(s => s.Expenses);
        if (summaryAmount > 0) result["Bank summaries"] = result.GetValueOrDefault("Bank summaries") + summaryAmount;
        return result;
    }

    static decimal ToMonthlyAmount(decimal amount, int billingIntervalDays) =>
        billingIntervalDays <= 0 ? amount : Math.Round(amount * 30.4375m / billingIntervalDays, 2);

    static FinanceAccountDto MapAccount(FinanceAccount account) =>
        new(account.Id, account.Name, account.Institution, account.Type, account.Currency,
            account.StartingBalance, account.Balance, account.Color, account.Notes,
            account.IsArchived, account.CreatedAt);

    static FinanceTransactionDto MapTransaction(FinanceTransaction transaction) =>
        new(transaction.Id, transaction.AccountId, transaction.Account?.Name,
            transaction.TransferAccountId, transaction.TransferAccount?.Name,
            transaction.Kind, transaction.Status, transaction.OccurredOn,
            transaction.Payee, transaction.Category, transaction.Amount,
            transaction.Description, transaction.Notes,
            string.IsNullOrWhiteSpace(transaction.TagsCsv) ? Array.Empty<string>() : transaction.TagsCsv.Split(','),
            transaction.CreatedAt, transaction.UpdatedAt);

    static MonthlyAccountSummaryDto MapMonthlySummary(MonthlyAccountSummary monthlySummary) =>
        new(monthlySummary.Id, monthlySummary.AccountId, monthlySummary.Account?.Name,
            monthlySummary.Account?.Currency ?? "CHF", monthlySummary.Month,
            monthlySummary.Income, monthlySummary.Expenses, monthlySummary.Income - monthlySummary.Expenses,
            monthlySummary.OpeningBalance, monthlySummary.ClosingBalance, monthlySummary.Notes,
            monthlySummary.CreatedAt, monthlySummary.UpdatedAt);

    static SubscriptionDto MapSubscription(Subscription subscription) =>
        new(subscription.Id, subscription.Name, subscription.Category, subscription.Provider,
            subscription.AccountId, subscription.Account?.Name, subscription.Amount, subscription.Currency,
            subscription.BillingIntervalDays, subscription.StartedOn, subscription.NextDueOn,
            subscription.AutoRenew, subscription.Status, subscription.Notes,
            ToMonthlyAmount(subscription.Amount, subscription.BillingIntervalDays),
            subscription.Attachments.Select(AttachmentEndpoints.MapAttachment).ToArray(),
            subscription.CreatedAt, subscription.UpdatedAt);

    static FinanceBudgetDto MapBudget(FinanceBudget budget, decimal spent)
    {
        var remaining = budget.LimitAmount - spent;
        var used = budget.LimitAmount <= 0 ? 0 : Math.Round(spent / budget.LimitAmount * 100, 1);
        return new FinanceBudgetDto(budget.Id, budget.Category, budget.Month, budget.LimitAmount,
            spent, remaining, used, budget.Notes, budget.CreatedAt, budget.UpdatedAt);
    }

    static AccountBalanceSnapshotDto MapBalanceSnapshot(AccountBalanceSnapshot snapshot) =>
        new(snapshot.Id, snapshot.AccountId, snapshot.Account?.Name, snapshot.Account?.Currency ?? "CHF",
            snapshot.SnapshotDate, snapshot.ActualBalance, snapshot.ExpectedBalance, snapshot.Difference,
            snapshot.IsReconciled, snapshot.Notes, snapshot.CreatedAt, snapshot.UpdatedAt);

    static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    static DateTime MonthStart(DateTime value) => new(value.Year, value.Month, 1);

    static string SummaryKey(int accountId, DateTime month) => $"{accountId}:{MonthStart(month):yyyy-MM-dd}";

    static bool HasSummaryFor(int accountId, DateTime date, HashSet<string> summaryKeys) =>
        summaryKeys.Contains(SummaryKey(accountId, date));
}
