using System.Net;
using System.Net.Http.Json;

namespace LuminaVault.Tests;

/// Settings drive the dropdowns the user sees when categorising items and transactions —
/// breakage here means the UI silently shows nothing. Cover create + uniqueness + seeds.
public class SettingsTests : IClassFixture<LuminaVaultFactory>
{
    private readonly ApiClient _api;

    public SettingsTests(LuminaVaultFactory factory)
    {
        _api = new ApiClient(factory.CreateClient());
    }

    private record CategoryDto(int Id, string Name, string Color, int SortOrder, DateTime CreatedAt);
    private record RuleDto(
        int Id, string Pattern, string Category, bool MatchPayee,
        bool MatchDescription, bool IsActive, int Priority);
    private record ExchangeRateDto(int Id, string Currency, decimal RateToBase, DateTime UpdatedAt);
    private record AccountDto(int Id, string Name);
    private record TransactionDto(int Id, string Payee, string Category);

    [Fact]
    public async Task Asset_categories_seed_with_expected_defaults()
    {
        // Seeder runs on first DB creation and inserts the canonical English categories.
        var cats = await _api.GetAsync<CategoryDto[]>("/api/settings/asset-categories");
        var names = cats!.Select(c => c.Name).ToHashSet();
        Assert.Contains("IT", names);
        Assert.Contains("Furniture", names);
        Assert.Contains("Other", names);
        Assert.DoesNotContain("Möbel", names); // German leftover from earlier seeds
    }

    [Fact]
    public async Task Create_asset_category_with_blank_name_returns_envelope()
    {
        var resp = await _api.PostAsync("/api/settings/asset-categories",
            new { name = "", color = "#7c3aed", sortOrder = 99 });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Contains("required", body!["error"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_asset_category_duplicate_returns_conflict()
    {
        await _api.PostAsync("/api/settings/asset-categories",
            new { name = "Unique-tag", color = "#7c3aed", sortOrder = 99 });
        var dup = await _api.PostAsync("/api/settings/asset-categories",
            new { name = "Unique-tag", color = "#7c3aed", sortOrder = 99 });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    [Fact]
    public async Task Finance_categories_seed_with_expected_defaults()
    {
        var cats = await _api.GetAsync<CategoryDto[]>("/api/settings/finance-categories");
        var names = cats!.Select(c => c.Name).ToHashSet();
        Assert.Contains("Salary", names);
        Assert.Contains("Subscriptions", names);
        Assert.Contains("Investments", names);
    }

    [Fact]
    public async Task Finance_category_rule_auto_categorizes_placeholder_transactions()
    {
        var ruleResp = await _api.PostAsync("/api/settings/finance-category-rules", new
        {
            pattern = "Migros",
            category = "Groceries",
            matchPayee = true,
            matchDescription = false,
            isActive = true,
            priority = 0,
        });
        ruleResp.EnsureSuccessStatusCode();
        var rule = await ruleResp.Content.ReadFromJsonAsync<RuleDto>();
        Assert.Equal("Groceries", rule!.Category);

        var accountResp = await _api.PostAsync("/api/finance/accounts/", new
        {
            name = "Rules account",
            institution = (string?)null,
            type = "Checking",
            currency = "CHF",
            startingBalance = 100m,
            balance = 100m,
            color = "#14b8a6",
            notes = (string?)null,
            isArchived = false,
        });
        accountResp.EnsureSuccessStatusCode();
        var account = await accountResp.Content.ReadFromJsonAsync<AccountDto>();

        var txResp = await _api.PostAsync("/api/finance/transactions/", new
        {
            accountId = account!.Id,
            transferAccountId = (int?)null,
            kind = "Expense",
            status = "Cleared",
            occurredOn = DateTime.UtcNow.Date,
            payee = "Migros Zürich",
            category = "General",
            amount = 12.30m,
            description = "",
            notes = "",
            tags = Array.Empty<string>(),
            symbol = (string?)null,
            quantity = (decimal?)null,
            pricePerUnit = (decimal?)null,
        });
        txResp.EnsureSuccessStatusCode();
        var transaction = await txResp.Content.ReadFromJsonAsync<TransactionDto>();
        Assert.Equal("Groceries", transaction!.Category);
    }

    [Fact]
    public async Task Exchange_rates_can_be_created_for_non_base_currencies()
    {
        var resp = await _api.PostAsync("/api/settings/exchange-rates", new
        {
            currency = "usd",
            rateToBase = 0.91m,
        });
        resp.EnsureSuccessStatusCode();
        var created = await resp.Content.ReadFromJsonAsync<ExchangeRateDto>();
        Assert.Equal("USD", created!.Currency);
        Assert.Equal(0.91m, created.RateToBase);

        var list = await _api.GetAsync<ExchangeRateDto[]>("/api/settings/exchange-rates");
        Assert.Contains(list!, r => r.Currency == "USD");
    }
}
