using System.Net;
using System.Net.Http.Json;

namespace LuminaVault.Tests;

/// Covers the load-bearing finance math: balance recompute after writes,
/// transfers debiting/crediting both sides, trade kinds aggregating into holdings.
public class FinanceFlowsTests : IClassFixture<LuminaVaultFactory>
{
    private readonly ApiClient _api;

    public FinanceFlowsTests(LuminaVaultFactory factory)
    {
        _api = new ApiClient(factory.CreateClient());
    }

    private record AccountDto(
        int Id, string Name, string? Institution, string Type, string Currency,
        decimal StartingBalance, decimal Balance, string Color, string? Notes,
        bool IsArchived, DateTime CreatedAt);

    private record HoldingDto(
        int Id, int AccountId, string? AccountName, string Currency, string Symbol, string? Name,
        decimal Quantity, decimal AverageCost, decimal? LastPrice, DateTime? LastPriceAt,
        string? ProviderId, decimal CostBasis, decimal? MarketValue, decimal? UnrealizedPnL,
        decimal? UnrealizedPnLPercent, decimal RealizedPnL, decimal Dividends, decimal Fees,
        decimal TotalReturn, string? Notes, DateTime CreatedAt, DateTime UpdatedAt);

    private record BudgetDto(
        int Id, string Category, DateTime Month, decimal LimitAmount, decimal Spent,
        decimal Remaining, decimal UsedPercent, string? Notes, DateTime CreatedAt, DateTime UpdatedAt);
    private record SummaryDto(decimal AccountNetWorth, string BaseCurrency, string[] FxMissingCurrencies);
    private record HoldingAnalyticsDto(
        string BaseCurrency,
        HoldingAnalyticsTotals Totals,
        HoldingAllocationDto[] AllocationBySymbol,
        HoldingPerformerDto[] TopPerformers);
    private record HoldingAnalyticsTotals(
        decimal MarketValue,
        decimal CostBasis,
        decimal UnrealizedPnL,
        decimal RealizedPnL,
        decimal Dividends,
        decimal Fees,
        decimal TotalReturn,
        decimal? TotalReturnPercent,
        int PositionCount);
    private record HoldingAllocationDto(string Name, decimal MarketValue, decimal CostBasis, decimal TotalReturn, decimal Percent);
    private record HoldingPerformerDto(string Symbol, decimal MarketValue, decimal CostBasis, decimal TotalReturn);
    private record SubscriptionDto(int Id, string Name, DateTime NextDueOn, string Status);
    private record GenerateDueDto(int Created, int Skipped, DateTime ThroughDate, TransactionDto[] Transactions, SubscriptionDto[] Subscriptions);
    private record GoalDto(
        int Id, string Name, int? AccountId, string? AccountName, string Currency,
        decimal TargetAmount, decimal CurrentAmount, decimal Remaining, decimal ProgressPercent,
        string Status);

    private async Task<AccountDto> CreateAccount(string name = "Main", decimal balance = 1000m, string type = "Checking")
    {
        var resp = await _api.PostAsync("/api/finance/accounts/", new
        {
            name,
            institution = (string?)null,
            type,
            currency = "CHF",
            startingBalance = balance,
            balance,
            color = "#14b8a6",
            notes = (string?)null,
            isArchived = false,
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AccountDto>())!;
    }

    [Fact]
    public async Task Create_expense_decreases_account_balance()
    {
        var account = await CreateAccount(balance: 500m);

        var post = await _api.PostAsync("/api/finance/transactions/", new
        {
            accountId = account.Id,
            transferAccountId = (int?)null,
            kind = "Expense",
            status = "Cleared",
            occurredOn = DateTime.UtcNow.Date,
            payee = "Groceries",
            category = "Food",
            amount = 60m,
            description = "",
            notes = "",
            tags = Array.Empty<string>(),
            symbol = (string?)null,
            quantity = (decimal?)null,
            pricePerUnit = (decimal?)null,
        });
        post.EnsureSuccessStatusCode();

        var list = await _api.GetAsync<AccountDto[]>("/api/finance/accounts/");
        var refreshed = list!.Single(a => a.Id == account.Id);
        Assert.Equal(440m, refreshed.Balance);
    }

    [Fact]
    public async Task Create_income_increases_account_balance()
    {
        var account = await CreateAccount(balance: 200m);

        var post = await _api.PostAsync("/api/finance/transactions/", new
        {
            accountId = account.Id,
            transferAccountId = (int?)null,
            kind = "Income",
            status = "Cleared",
            occurredOn = DateTime.UtcNow.Date,
            payee = "Salary",
            category = "Salary",
            amount = 4500m,
            description = "",
            notes = "",
            tags = Array.Empty<string>(),
            symbol = (string?)null,
            quantity = (decimal?)null,
            pricePerUnit = (decimal?)null,
        });
        post.EnsureSuccessStatusCode();

        var refreshed = (await _api.GetAsync<AccountDto[]>("/api/finance/accounts/"))!
            .Single(a => a.Id == account.Id);
        Assert.Equal(4700m, refreshed.Balance);
    }

    [Fact]
    public async Task Transfer_debits_source_and_credits_destination()
    {
        var checking = await CreateAccount("Checking", 1000m);
        var savings = await CreateAccount("Savings", 0m, type: "Savings");

        var post = await _api.PostAsync("/api/finance/transactions/", new
        {
            accountId = checking.Id,
            transferAccountId = savings.Id,
            kind = "Transfer",
            status = "Cleared",
            occurredOn = DateTime.UtcNow.Date,
            payee = "Move to savings",
            category = "Transfer",
            amount = 250m,
            description = "",
            notes = "",
            tags = Array.Empty<string>(),
            symbol = (string?)null,
            quantity = (decimal?)null,
            pricePerUnit = (decimal?)null,
        });
        post.EnsureSuccessStatusCode();

        var accounts = (await _api.GetAsync<AccountDto[]>("/api/finance/accounts/"))!;
        Assert.Equal(750m, accounts.Single(a => a.Id == checking.Id).Balance);
        Assert.Equal(250m, accounts.Single(a => a.Id == savings.Id).Balance);
    }

    [Fact]
    public async Task Delete_transaction_restores_balance()
    {
        var account = await CreateAccount(balance: 500m);

        var post = await _api.PostAsync("/api/finance/transactions/", new
        {
            accountId = account.Id,
            transferAccountId = (int?)null,
            kind = "Expense",
            status = "Cleared",
            occurredOn = DateTime.UtcNow.Date,
            payee = "Books",
            category = "Hobby",
            amount = 80m,
            description = "",
            notes = "",
            tags = Array.Empty<string>(),
            symbol = (string?)null,
            quantity = (decimal?)null,
            pricePerUnit = (decimal?)null,
        });
        var created = await post.Content.ReadFromJsonAsync<TransactionDto>();

        var del = await _api.DeleteAsync($"/api/finance/transactions/{created!.Id}");
        del.EnsureSuccessStatusCode();

        var refreshed = (await _api.GetAsync<AccountDto[]>("/api/finance/accounts/"))!
            .Single(a => a.Id == account.Id);
        Assert.Equal(500m, refreshed.Balance);
    }

    [Fact]
    public async Task Buy_then_buy_computes_weighted_average_cost()
    {
        var account = await CreateAccount("Brokerage", balance: 10000m, type: "Investment");

        // 10 shares @ 100 → avg 100
        var buy1 = await _api.PostAsync("/api/finance/transactions/", new
        {
            accountId = account.Id,
            transferAccountId = (int?)null,
            kind = "Buy",
            status = "Cleared",
            occurredOn = DateTime.UtcNow.Date.AddDays(-2),
            payee = "VTI buy",
            category = "Investments",
            amount = 1000m,
            description = "",
            notes = "",
            tags = Array.Empty<string>(),
            symbol = "VTI",
            quantity = 10m,
            pricePerUnit = 100m,
        });
        buy1.EnsureSuccessStatusCode();

        // 10 more shares @ 120 → new avg = (10*100 + 10*120) / 20 = 110
        var buy2 = await _api.PostAsync("/api/finance/transactions/", new
        {
            accountId = account.Id,
            transferAccountId = (int?)null,
            kind = "Buy",
            status = "Cleared",
            occurredOn = DateTime.UtcNow.Date.AddDays(-1),
            payee = "VTI buy",
            category = "Investments",
            amount = 1200m,
            description = "",
            notes = "",
            tags = Array.Empty<string>(),
            symbol = "VTI",
            quantity = 10m,
            pricePerUnit = 120m,
        });
        buy2.EnsureSuccessStatusCode();

        var holdings = await _api.GetAsync<HoldingDto[]>("/api/finance/holdings/");
        var vti = holdings!.Single(h => h.Symbol == "VTI");
        Assert.Equal(20m, vti.Quantity);
        Assert.Equal(110m, vti.AverageCost);
    }

    [Fact]
    public async Task Sell_all_zeroes_the_holding()
    {
        var account = await CreateAccount("Brokerage", balance: 10000m, type: "Investment");

        await _api.PostAsync("/api/finance/transactions/", new
        {
            accountId = account.Id,
            transferAccountId = (int?)null,
            kind = "Buy",
            status = "Cleared",
            occurredOn = DateTime.UtcNow.Date.AddDays(-1),
            payee = "Buy",
            category = "Investments",
            amount = 500m,
            description = "",
            notes = "",
            tags = Array.Empty<string>(),
            symbol = "AAPL",
            quantity = 5m,
            pricePerUnit = 100m,
        });

        await _api.PostAsync("/api/finance/transactions/", new
        {
            accountId = account.Id,
            transferAccountId = (int?)null,
            kind = "Sell",
            status = "Cleared",
            occurredOn = DateTime.UtcNow.Date,
            payee = "Sell",
            category = "Investments",
            amount = 600m,
            description = "",
            notes = "",
            tags = Array.Empty<string>(),
            symbol = "AAPL",
            quantity = 5m,
            pricePerUnit = 120m,
        });

        var holdings = (await _api.GetAsync<HoldingDto[]>("/api/finance/holdings/"))!;
        var aapl = holdings.Single(h => h.Symbol == "AAPL");
        Assert.Equal(0m, aapl.Quantity);
        Assert.Equal(0m, aapl.AverageCost);
        Assert.Equal(100m, aapl.RealizedPnL);
        Assert.Equal(100m, aapl.TotalReturn);
    }

    [Fact]
    public async Task Holdings_include_realized_dividend_fee_and_total_return()
    {
        var account = await CreateAccount("Performance", balance: 10000m, type: "Investment");

        foreach (var tx in new[]
        {
            new { Kind = "Buy", Amount = 1000m, Symbol = "MSFT", Quantity = 10m, Price = 100m },
            new { Kind = "Sell", Amount = 600m, Symbol = "MSFT", Quantity = 5m, Price = 120m },
            new { Kind = "Dividend", Amount = 20m, Symbol = "MSFT", Quantity = 0m, Price = 0m },
            new { Kind = "Fee", Amount = 2m, Symbol = "MSFT", Quantity = 0m, Price = 0m },
        })
        {
            var resp = await _api.PostAsync("/api/finance/transactions/", new
            {
                accountId = account.Id,
                transferAccountId = (int?)null,
                kind = tx.Kind,
                status = "Cleared",
                occurredOn = DateTime.UtcNow.Date,
                payee = tx.Kind,
                category = "Investments",
                amount = tx.Amount,
                description = "",
                notes = "",
                tags = Array.Empty<string>(),
                symbol = tx.Symbol,
                quantity = tx.Quantity == 0 ? (decimal?)null : tx.Quantity,
                pricePerUnit = tx.Price == 0 ? (decimal?)null : tx.Price,
            });
            resp.EnsureSuccessStatusCode();
        }

        var holdings = (await _api.GetAsync<HoldingDto[]>("/api/finance/holdings/"))!;
        var msft = holdings.Single(h => h.Symbol == "MSFT");
        Assert.Equal(100m, msft.RealizedPnL);
        Assert.Equal(20m, msft.Dividends);
        Assert.Equal(2m, msft.Fees);
        Assert.Equal(118m, msft.TotalReturn);

        var analytics = await _api.GetAsync<HoldingAnalyticsDto>($"/api/finance/holdings/analytics?accountId={account.Id}");
        Assert.Equal("CHF", analytics!.BaseCurrency);
        Assert.Equal(1, analytics.Totals.PositionCount);
        Assert.Equal(500m, analytics.Totals.CostBasis);
        Assert.Equal(118m, analytics.Totals.TotalReturn);
        Assert.Contains(analytics.AllocationBySymbol, r => r.Name == "MSFT");
        Assert.Equal("MSFT", analytics.TopPerformers.Single().Symbol);
    }

    [Fact]
    public async Task Generate_due_subscriptions_creates_pending_forecasts_and_advances_due_date()
    {
        var account = await CreateAccount("Subscription account", balance: 500m);
        var dueDate = DateTime.UtcNow.Date.AddDays(2);
        var create = await _api.PostAsync("/api/finance/subscriptions/", new
        {
            name = "Streaming",
            category = "Subscriptions",
            provider = "StreamCo",
            accountId = account.Id,
            amount = 12.99m,
            currency = "CHF",
            billingIntervalDays = 30,
            startedOn = dueDate.AddMonths(-1),
            nextDueOn = dueDate,
            autoRenew = true,
            status = "Active",
            notes = "Family plan",
        });
        create.EnsureSuccessStatusCode();

        var generated = await _api.PostAsync("/api/finance/subscriptions/generate-due?lookAheadDays=7", new { });
        generated.EnsureSuccessStatusCode();
        var result = await generated.Content.ReadFromJsonAsync<GenerateDueDto>();

        Assert.Equal(1, result!.Created);
        Assert.Equal(12.99m, result.Transactions.Single().Amount);
        Assert.Equal("Expense", result.Transactions.Single().Kind);
        Assert.Equal(dueDate.AddDays(30), result.Subscriptions.Single().NextDueOn.Date);

        var tx = await _api.GetAsync<TransactionDto[]>("/api/finance/transactions/?q=subscription");
        Assert.Single(tx!);
    }

    [Fact]
    public async Task Summary_converts_account_balances_to_base_currency()
    {
        var before = await _api.GetAsync<SummaryDto>("/api/finance/summary");
        await _api.PostAsync("/api/settings/exchange-rates", new
        {
            currency = "GBP",
            rateToBase = 1.10m,
        });
        await CreateAccount("GBP account", balance: 100m, type: "Checking");
        var accounts = await _api.GetAsync<AccountDto[]>("/api/finance/accounts/");
        var gbpAccount = accounts!.Single(a => a.Name == "GBP account");

        await _api.PutAsync($"/api/finance/accounts/{gbpAccount.Id}", new
        {
            name = "GBP account",
            institution = (string?)null,
            type = "Checking",
            currency = "GBP",
            startingBalance = 100m,
            balance = 100m,
            color = "#14b8a6",
            notes = (string?)null,
            isArchived = false,
        });

        var summary = await _api.GetAsync<SummaryDto>("/api/finance/summary");
        Assert.Equal("CHF", summary!.BaseCurrency);
        Assert.Equal((before?.AccountNetWorth ?? 0m) + 110m, summary.AccountNetWorth);
        Assert.Empty(summary.FxMissingCurrencies);
    }

    [Fact]
    public async Task Budget_spent_reflects_matching_transactions()
    {
        var account = await CreateAccount(balance: 1000m);
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        await _api.PostAsync("/api/finance/budgets/", new
        {
            category = "Food",
            month = monthStart,
            limitAmount = 400m,
            notes = (string?)null,
        });

        // Two food expenses in the same month
        foreach (var amount in new[] { 60m, 35m })
        {
            await _api.PostAsync("/api/finance/transactions/", new
            {
                accountId = account.Id,
                transferAccountId = (int?)null,
                kind = "Expense",
                status = "Cleared",
                occurredOn = monthStart.AddDays(5),
                payee = "Grocery",
                category = "Food",
                amount,
                description = "",
                notes = "",
                tags = Array.Empty<string>(),
                symbol = (string?)null,
                quantity = (decimal?)null,
                pricePerUnit = (decimal?)null,
            });
        }

        var budgets = await _api.GetAsync<BudgetDto[]>($"/api/finance/budgets/?month={monthStart:yyyy-MM-dd}");
        var food = budgets!.Single(b => b.Category == "Food");
        Assert.Equal(95m, food.Spent);
        Assert.Equal(305m, food.Remaining);
    }

    [Fact]
    public async Task Savings_goal_reports_progress_and_remaining()
    {
        var account = await CreateAccount("Goal account", balance: 500m, type: "Savings");
        var resp = await _api.PostAsync("/api/finance/goals/", new
        {
            name = "Emergency fund",
            accountId = account.Id,
            currency = "CHF",
            targetAmount = 1000m,
            currentAmount = 250m,
            targetDate = DateTime.UtcNow.Date.AddMonths(6),
            status = "Active",
            notes = "Three months runway",
        });
        resp.EnsureSuccessStatusCode();
        var created = await resp.Content.ReadFromJsonAsync<GoalDto>();
        Assert.Equal(750m, created!.Remaining);
        Assert.Equal(25m, created.ProgressPercent);

        var list = await _api.GetAsync<GoalDto[]>("/api/finance/goals/");
        var goal = list!.Single(g => g.Name == "Emergency fund");
        Assert.Equal("Goal account", goal.AccountName);
    }

    private record TransactionDto(int Id, int AccountId, decimal Amount, string Kind, SplitDto[] Splits);
    private record SplitDto(int Id, string Category, decimal Amount, string? Notes, int SortOrder);

    [Fact]
    public async Task Transaction_can_be_saved_with_splits_that_redirect_budget_spend()
    {
        var account = await CreateAccount("Split account", balance: 500m);
        var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        // Use unique category names so the shared SQLite fixture doesn't bleed into
        // other tests' budget tallies (e.g. the existing Food budget test).
        await _api.PostAsync("/api/finance/budgets/", new
        {
            category = "SplitHousehold", month, limitAmount = 50m, notes = (string?)null,
        });

        var resp = await _api.PostAsync("/api/finance/transactions/", new
        {
            accountId = account.Id, transferAccountId = (int?)null,
            kind = "Expense", status = "Cleared", occurredOn = DateTime.UtcNow.Date,
            payee = "Grocery", category = "SplitMisc", amount = 100m,
            description = (string?)null, notes = (string?)null,
            tags = Array.Empty<string>(),
            splits = new[]
            {
                new { category = "SplitFood", amount = 70m, notes = (string?)null },
                new { category = "SplitHousehold", amount = 30m, notes = (string?)null },
            },
        });
        resp.EnsureSuccessStatusCode();
        var saved = await resp.Content.ReadFromJsonAsync<TransactionDto>();
        Assert.Equal(2, saved!.Splits.Length);
        Assert.Contains(saved.Splits, s => s.Category == "SplitFood" && s.Amount == 70m);
        Assert.Contains(saved.Splits, s => s.Category == "SplitHousehold" && s.Amount == 30m);

        // The SplitHousehold budget (CHF 50) should now show CHF 30 spent — not CHF 100 —
        // because the split-aware aggregator routes the CHF 30 portion of the receipt to
        // SplitHousehold even though the transaction's headline category is "SplitMisc".
        var budgets = await _api.GetAsync<BudgetDto[]>($"/api/finance/budgets/?month={month:yyyy-MM-dd}");
        var household = budgets!.Single(b => b.Category == "SplitHousehold");
        Assert.Equal(30m, household.Spent);
        Assert.Equal(20m, household.Remaining);
    }

    [Fact]
    public async Task Splits_that_dont_sum_to_amount_are_rejected()
    {
        var account = await CreateAccount("Bad split", balance: 500m);
        var resp = await _api.PostAsync("/api/finance/transactions/", new
        {
            accountId = account.Id, transferAccountId = (int?)null,
            kind = "Expense", status = "Cleared", occurredOn = DateTime.UtcNow.Date,
            payee = "Mismatch", category = "General", amount = 100m,
            description = (string?)null, notes = (string?)null,
            tags = Array.Empty<string>(),
            splits = new[]
            {
                new { category = "Food", amount = 30m, notes = (string?)null },
                new { category = "Household", amount = 40m, notes = (string?)null },
            },
        });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Transfers_cannot_be_split()
    {
        var src = await CreateAccount("Transfer src", balance: 500m);
        var dest = await CreateAccount("Transfer dest", balance: 0m);
        var resp = await _api.PostAsync("/api/finance/transactions/", new
        {
            accountId = src.Id, transferAccountId = (int?)dest.Id,
            kind = "Transfer", status = "Cleared", occurredOn = DateTime.UtcNow.Date,
            payee = "Move", category = "General", amount = 50m,
            description = (string?)null, notes = (string?)null,
            tags = Array.Empty<string>(),
            splits = new[] { new { category = "Food", amount = 50m, notes = (string?)null } },
        });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Editing_a_transaction_replaces_its_splits()
    {
        var account = await CreateAccount("Edit splits", balance: 500m);
        var create = await _api.PostAsync("/api/finance/transactions/", new
        {
            accountId = account.Id, transferAccountId = (int?)null,
            kind = "Expense", status = "Cleared", occurredOn = DateTime.UtcNow.Date,
            payee = "First", category = "General", amount = 100m,
            description = (string?)null, notes = (string?)null,
            tags = Array.Empty<string>(),
            splits = new[]
            {
                new { category = "Food", amount = 60m, notes = (string?)null },
                new { category = "Household", amount = 40m, notes = (string?)null },
            },
        });
        var initial = await create.Content.ReadFromJsonAsync<TransactionDto>();
        Assert.Equal(2, initial!.Splits.Length);

        var update = await _api.PutAsync($"/api/finance/transactions/{initial.Id}", new
        {
            accountId = account.Id, transferAccountId = (int?)null,
            kind = "Expense", status = "Cleared", occurredOn = DateTime.UtcNow.Date,
            payee = "First", category = "General", amount = 100m,
            description = (string?)null, notes = (string?)null,
            tags = Array.Empty<string>(),
            splits = new[]
            {
                new { category = "Hobby", amount = 100m, notes = (string?)null },
            },
        });
        update.EnsureSuccessStatusCode();
        var updated = await update.Content.ReadFromJsonAsync<TransactionDto>();
        Assert.Single(updated!.Splits);
        Assert.Equal("Hobby", updated.Splits[0].Category);
    }

    private record ForecastPointDto(DateTime Date, decimal Balance, decimal ChangeFromYesterday);
    private record ForecastEventDto(DateTime Date, string Source, string Description, int? AccountId, string? AccountName, decimal Amount, string Currency, decimal BaseAmount);
    private record ForecastDto(
        DateTime From, DateTime To, int Days, string BaseCurrency,
        decimal StartingBalance, decimal EndingBalance, decimal NetChange,
        decimal LowestBalance, DateTime LowestDate, int EventCount,
        ForecastPointDto[] Daily, ForecastEventDto[] Events);

    [Fact]
    public async Task Forecast_with_no_scheduled_events_is_flat()
    {
        var account = await CreateAccount("Quiet account", balance: 1234.56m);

        var forecast = await _api.GetAsync<ForecastDto>("/api/finance/forecast?days=30&accountId=" + account.Id);

        Assert.NotNull(forecast);
        Assert.Equal(30, forecast!.Days);
        Assert.Equal(31, forecast.Daily.Length); // today + 30 future days
        Assert.Empty(forecast.Events);
        Assert.Equal(0, forecast.EventCount);
        Assert.Equal(forecast.StartingBalance, forecast.EndingBalance);
        Assert.Equal(0m, forecast.NetChange);
        // Every day equals the starting balance — flat line.
        Assert.All(forecast.Daily, p => Assert.Equal(forecast.StartingBalance, p.Balance));
    }

    [Fact]
    public async Task Forecast_replays_pending_expense_on_its_due_date()
    {
        var account = await CreateAccount("Forecast account", balance: 1000m);
        var dueDate = DateTime.UtcNow.Date.AddDays(5);
        var pending = await _api.PostAsync("/api/finance/transactions/", new
        {
            accountId = account.Id,
            transferAccountId = (int?)null,
            kind = "Expense",
            status = "Pending",
            occurredOn = dueDate,
            payee = "Future rent",
            category = "Housing",
            amount = 250m,
            description = (string?)null,
            notes = (string?)null,
            tags = Array.Empty<string>(),
        });
        pending.EnsureSuccessStatusCode();

        var forecast = await _api.GetAsync<ForecastDto>("/api/finance/forecast?days=30&accountId=" + account.Id);

        Assert.NotNull(forecast);
        Assert.Equal(1000m, forecast!.StartingBalance);
        Assert.Equal(750m, forecast.EndingBalance);
        Assert.Equal(-250m, forecast.NetChange);
        Assert.Equal(750m, forecast.LowestBalance);
        Assert.Equal(dueDate, forecast.LowestDate.Date);

        var evt = Assert.Single(forecast.Events);
        Assert.Equal("Pending", evt.Source);
        Assert.Equal(-250m, evt.Amount);
        Assert.Equal(dueDate, evt.Date.Date);

        // Day-by-day: balance stays at 1000 up to (but not including) dueDate, then drops.
        var beforeDip = forecast.Daily.First(p => p.Date.Date == dueDate.AddDays(-1));
        var atDip = forecast.Daily.First(p => p.Date.Date == dueDate);
        Assert.Equal(1000m, beforeDip.Balance);
        Assert.Equal(750m, atDip.Balance);
    }

    [Fact]
    public async Task Forecast_rolls_subscriptions_forward_and_deduplicates_against_generated_pending()
    {
        var account = await CreateAccount("Sub account", balance: 1000m);
        var firstDue = DateTime.UtcNow.Date.AddDays(3);
        await _api.PostAsync("/api/finance/subscriptions/", new
        {
            name = "Hosting",
            category = "Subscriptions",
            provider = (string?)null,
            accountId = account.Id,
            amount = 20m,
            currency = "CHF",
            billingIntervalDays = 30,
            startedOn = firstDue.AddDays(-30),
            nextDueOn = firstDue,
            autoRenew = true,
            status = "Active",
            notes = (string?)null,
        });

        // 60-day window should project two billing cycles (day 3 and day 33).
        var forecast = await _api.GetAsync<ForecastDto>("/api/finance/forecast?days=60&accountId=" + account.Id);
        Assert.NotNull(forecast);
        Assert.Equal(2, forecast!.Events.Length);
        Assert.All(forecast.Events, e => Assert.Equal("Subscription", e.Source));
        Assert.Equal(960m, forecast.EndingBalance);  // 1000 - 20 - 20
        Assert.Equal(-40m, forecast.NetChange);

        // Generate the next due as a tagged pending tx — forecast must deduplicate it
        // so the cycle isn't double-counted (once as Pending, once as Subscription).
        var generated = await _api.PostAsync("/api/finance/subscriptions/generate-due?lookAheadDays=5", new { });
        generated.EnsureSuccessStatusCode();

        var refreshed = await _api.GetAsync<ForecastDto>("/api/finance/forecast?days=60&accountId=" + account.Id);
        Assert.Equal(2, refreshed!.Events.Length);
        Assert.Equal(-40m, refreshed.NetChange);
        // One event is now a Pending row (the auto-generated one), the other is still a rolled-forward Subscription.
        Assert.Contains(refreshed.Events, e => e.Source == "Pending");
        Assert.Contains(refreshed.Events, e => e.Source == "Subscription");
    }
}
