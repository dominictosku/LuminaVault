using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace LuminaVault.Tests;

/// Year-end tax bundle: validates the holdings replay (Buy/Sell up to Dec 31 only),
/// account balance snapshot, and income/expense category aggregation. The bundle is
/// the thing a Swiss user pastes into the Wertschriftenverzeichnis, so getting the
/// "as of Dec 31" cutoff right is the key invariant under test.
public class TaxExportTests : IClassFixture<LuminaVaultFactory>
{
    private readonly ApiClient _api;

    public TaxExportTests(LuminaVaultFactory factory)
    {
        _api = new ApiClient(factory.CreateClient());
    }

    private record AccountDto(int Id, string Name);

    private record TaxHoldingRow(
        string Account, string Symbol, string? Name, decimal Quantity,
        [property: JsonPropertyName("averageCost")] decimal AvgCost,
        decimal CostBasis, decimal? PriceAtYearEnd, DateTime? PriceAsOf,
        bool PriceIsStale, decimal? EstimatedValue, string Currency);

    private record TaxAccountBalanceRow(
        string Account, string Currency, string Type, decimal BalanceAtYearEnd);

    private record TaxCategoryTotalRow(string Category, decimal Amount);

    private record TaxBundleDto(
        int Year, DateTime AsOf,
        TaxHoldingRow[] Holdings, decimal TotalSecuritiesValue,
        TaxAccountBalanceRow[] AccountBalances, decimal TotalCashBalance,
        TaxCategoryTotalRow[] Income, decimal TotalIncome,
        TaxCategoryTotalRow[] Expenses, decimal TotalExpenses,
        string[] Warnings);

    [Fact]
    public async Task Year_end_bundle_freezes_holdings_at_dec_31()
    {
        await _api.EnsureAuthedAsync();
        var investment = await CreateAccount("Tax-Investment", "Investment");
        var cash = await CreateAccount("Tax-Cash", "Checking");

        // Buys before year-end, partial sell mid-year, buy AFTER year-end that
        // should NOT influence the snapshot.
        await PostTransaction(investment.Id, "Buy", "2025-03-15", payee: "Buy A early", amount: 1000m,
            symbol: "TAXA", quantity: 10m, pricePerUnit: 100m, category: "Investment");
        await PostTransaction(investment.Id, "Buy", "2025-09-01", payee: "Buy A more", amount: 1200m,
            symbol: "TAXA", quantity: 10m, pricePerUnit: 120m, category: "Investment");
        await PostTransaction(investment.Id, "Sell", "2025-11-30", payee: "Sell A", amount: 600m,
            symbol: "TAXA", quantity: 5m, pricePerUnit: 130m, category: "Investment");
        // Post-year-end buy of a *different* symbol — must be excluded.
        await PostTransaction(investment.Id, "Buy", "2026-01-20", payee: "Buy B later", amount: 500m,
            symbol: "TAXB", quantity: 5m, pricePerUnit: 100m, category: "Investment");

        // Cash side: income/expense across years so we can assert year-only aggregation.
        await PostTransaction(cash.Id, "Income", "2025-06-01", payee: "Salary", amount: 5000m, category: "Salary");
        await PostTransaction(cash.Id, "Expense", "2025-07-15", payee: "Rent", amount: 1500m, category: "Rent");
        await PostTransaction(cash.Id, "Income", "2026-01-05", payee: "Salary 2026", amount: 5200m, category: "Salary");

        var bundle = await GetBundle(2025);

        Assert.Equal(2025, bundle.Year);
        Assert.Equal(new DateTime(2025, 12, 31), bundle.AsOf.Date);

        // Year-end TAXA: 10 + 10 − 5 = 15 units. The post-year buy of TAXB is excluded.
        var taxa = Assert.Single(bundle.Holdings, h => h.Symbol == "TAXA");
        Assert.Equal(15m, taxa.Quantity);
        Assert.DoesNotContain(bundle.Holdings, h => h.Symbol == "TAXB");

        // Income/expense buckets only include the 2025 rows.
        Assert.Equal(5000m, bundle.TotalIncome);
        Assert.Equal(1500m, bundle.TotalExpenses);
        Assert.Contains(bundle.Income, i => i.Category == "Salary" && i.Amount == 5000m);
        Assert.Contains(bundle.Expenses, e => e.Category == "Rent" && e.Amount == 1500m);

        // Both accounts show up in the balances; investment shows 0 cash (no cash tx),
        // checking shows income − expenses (5000 − 1500 = 3500) over its starting balance.
        var cashRow = Assert.Single(bundle.AccountBalances, a => a.Account == "Tax-Cash");
        Assert.Equal(1000m + 5000m - 1500m, cashRow.BalanceAtYearEnd);

        // No prices recorded for TAXA → warning row + null estimated value.
        Assert.Contains(bundle.Warnings, w => w.Contains("TAXA"));
        Assert.Null(taxa.EstimatedValue);
    }

    [Fact]
    public async Task Symbols_sold_to_zero_before_year_end_are_excluded()
    {
        await _api.EnsureAuthedAsync();
        var investment = await CreateAccount("Tax-Zero", "Investment");

        await PostTransaction(investment.Id, "Buy", "2025-02-01", payee: "Buy zero", amount: 100m,
            symbol: "ZERO", quantity: 1m, pricePerUnit: 100m, category: "Investment");
        await PostTransaction(investment.Id, "Sell", "2025-06-01", payee: "Sell all", amount: 150m,
            symbol: "ZERO", quantity: 1m, pricePerUnit: 150m, category: "Investment");

        var bundle = await GetBundle(2025);
        Assert.DoesNotContain(bundle.Holdings, h => h.Symbol == "ZERO");
    }

    [Fact]
    public async Task Ods_endpoint_returns_a_valid_workbook()
    {
        await _api.EnsureAuthedAsync();
        await CreateAccount("Tax-Ods", "Checking");
        var resp = await _api.Raw.GetAsync("/api/finance/tax-export/ods?year=2025");
        resp.EnsureSuccessStatusCode();
        Assert.Equal("application/vnd.oasis.opendocument.spreadsheet",
            resp.Content.Headers.ContentType?.MediaType);
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 100);
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    async Task<AccountDto> CreateAccount(string name, string type)
    {
        var resp = await _api.PostAsync("/api/finance/accounts/", new
        {
            name, institution = (string?)null, type, currency = "CHF",
            startingBalance = 1000m, balance = 1000m, color = "#14b8a6",
            notes = (string?)null, isArchived = false,
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AccountDto>())!;
    }

    async Task PostTransaction(
        int accountId, string kind, string date, string payee,
        decimal amount, string category, string? symbol = null,
        decimal? quantity = null, decimal? pricePerUnit = null)
    {
        var resp = await _api.PostAsync("/api/finance/transactions/", new
        {
            accountId, transferAccountId = (int?)null, kind, status = "Cleared",
            occurredOn = date, payee, category, amount,
            description = (string?)null, notes = (string?)null, tags = Array.Empty<string>(),
            symbol, quantity, pricePerUnit, splits = (object?)null,
        });
        resp.EnsureSuccessStatusCode();
    }

    async Task<TaxBundleDto> GetBundle(int year)
    {
        var resp = await _api.Raw.GetAsync($"/api/finance/tax-export/?year={year}");
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<TaxBundleDto>())!;
    }
}
