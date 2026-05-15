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
        decimal? UnrealizedPnLPercent, string? Notes, DateTime CreatedAt, DateTime UpdatedAt);

    private record BudgetDto(
        int Id, string Category, DateTime Month, decimal LimitAmount, decimal Spent,
        decimal Remaining, decimal UsedPercent, string? Notes, DateTime CreatedAt, DateTime UpdatedAt);

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

    private record TransactionDto(int Id, int AccountId, decimal Amount, string Kind);
}
