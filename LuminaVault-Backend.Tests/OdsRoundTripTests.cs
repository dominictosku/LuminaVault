using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace LuminaVault.Tests;

/// Round-trip integrity for the ODS export/import pipeline — the path users hit when
/// migrating between machines or re-importing a budget spreadsheet from LibreOffice.
/// We assert two properties: the export is a non-empty ODS file, and re-importing it
/// is idempotent for entities that have natural keys (accounts, categories, subscriptions).
public class OdsRoundTripTests : IClassFixture<LuminaVaultFactory>
{
    private readonly ApiClient _api;

    public OdsRoundTripTests(LuminaVaultFactory factory)
    {
        _api = new ApiClient(factory.CreateClient());
    }

    private record AccountDto(int Id, string Name);

    private record OdsImportResult(
        [property: JsonPropertyName("accounts")] int Accounts,
        [property: JsonPropertyName("transactions")] int Transactions,
        [property: JsonPropertyName("holdings")] int Holdings,
        [property: JsonPropertyName("monthlySummaries")] int MonthlySummaries,
        [property: JsonPropertyName("subscriptions")] int Subscriptions,
        [property: JsonPropertyName("financeCategories")] int FinanceCategories,
        [property: JsonPropertyName("assetCategories")] int AssetCategories,
        [property: JsonPropertyName("assets")] int Assets,
        [property: JsonPropertyName("warnings")] string[] Warnings);

    private record SplitDto(int Id, string Category, decimal Amount, string? Notes, int SortOrder);
    private record TransactionDto(int Id, string Payee, string Category, decimal Amount, SplitDto[] Splits);

    [Fact]
    public async Task Export_returns_non_empty_ods_with_correct_content_type()
    {
        await SeedOneAccount();

        await _api.EnsureAuthedAsync();
        var resp = await _api.Raw.GetAsync("/api/data/export/ods");
        resp.EnsureSuccessStatusCode();
        Assert.Equal("application/vnd.oasis.opendocument.spreadsheet",
            resp.Content.Headers.ContentType?.MediaType);

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 100, "Expected an ODS file with at least some content");
        // ODS files are ZIP archives — first two bytes are "PK".
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]
    public async Task Reimporting_the_export_is_idempotent_for_entities_with_natural_keys()
    {
        await SeedOneAccount();

        // Round-trip: export, then re-import the same file. Accounts dedupe by name,
        // so we expect zero new accounts inserted on the second pass.
        await _api.EnsureAuthedAsync();
        var exportResp = await _api.Raw.GetAsync("/api/data/export/ods");
        exportResp.EnsureSuccessStatusCode();
        var bytes = await exportResp.Content.ReadAsByteArrayAsync();

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.oasis.opendocument.spreadsheet");
        form.Add(fileContent, "file", "export.ods");

        var importResp = await _api.Raw.PostAsync("/api/data/import/ods", form);
        Assert.Equal(HttpStatusCode.OK, importResp.StatusCode);
        var result = await importResp.Content.ReadFromJsonAsync<OdsImportResult>();

        // Account "RT-Test" was already in the DB → zero new accounts.
        // Seeded categories were already present → zero new categories.
        Assert.Equal(0, result!.Accounts);
        Assert.Equal(0, result.FinanceCategories);
        Assert.Equal(0, result.AssetCategories);
        // Transactions don't have natural-key dedup yet — that's expected and documented.
        // The other counts can be anything; just assert no crashy warnings.
        Assert.DoesNotContain(result.Warnings, w => w.Contains("Exception", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Transaction_splits_round_trip_through_export_and_reimport()
    {
        await _api.EnsureAuthedAsync();
        var account = await SeedNamedAccount("Split-RT");
        // Use unique categories so the splits stay attributable when re-imported
        // alongside other transactions sharing common categories.
        var postResp = await _api.PostAsync("/api/finance/transactions/", new
        {
            accountId = account.Id,
            transferAccountId = (int?)null,
            kind = "Expense",
            status = "Cleared",
            occurredOn = "2026-04-12",
            payee = "Split-RT-Grocery",
            category = "RT-Food",
            amount = 100m,
            description = (string?)null,
            notes = (string?)null,
            tags = Array.Empty<string>(),
            symbol = (string?)null,
            quantity = (decimal?)null,
            pricePerUnit = (decimal?)null,
            splits = new object[]
            {
                new { category = "RT-Food", amount = 70m, notes = (string?)"groceries" },
                new { category = "RT-Household", amount = 30m, notes = (string?)null },
            }
        });
        postResp.EnsureSuccessStatusCode();

        var exportResp = await _api.Raw.GetAsync("/api/data/export/ods");
        exportResp.EnsureSuccessStatusCode();
        var bytes = await exportResp.Content.ReadAsByteArrayAsync();

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.oasis.opendocument.spreadsheet");
        form.Add(fileContent, "file", "export.ods");
        var importResp = await _api.Raw.PostAsync("/api/data/import/ods", form);
        importResp.EnsureSuccessStatusCode();

        // After re-import, two transactions with payee "Split-RT-Grocery" exist
        // (transactions don't dedupe). Both should carry the same 2-row split set.
        var listResp = await _api.Raw.GetAsync("/api/finance/transactions/?q=Split-RT-Grocery");
        listResp.EnsureSuccessStatusCode();
        var page = await listResp.Content.ReadFromJsonAsync<TransactionPage>();
        Assert.NotNull(page);
        var list = page!.Items;
        Assert.True(list.Length >= 2, $"expected at least 2 round-tripped rows, got {list.Length}");
        foreach (var tx in list)
        {
            Assert.Equal(2, tx.Splits.Length);
            var food = tx.Splits.Single(s => s.Category == "RT-Food");
            var household = tx.Splits.Single(s => s.Category == "RT-Household");
            Assert.Equal(70m, food.Amount);
            Assert.Equal("groceries", food.Notes);
            Assert.Equal(30m, household.Amount);
            Assert.Null(household.Notes);
        }
    }

    [Fact]
    public async Task Splits_with_a_bad_sum_drop_silently_and_warn()
    {
        // Hand-craft a minimal ODS so we can inject malformed split data without
        // going through the validated POST endpoint.
        await _api.EnsureAuthedAsync();
        var account = await SeedNamedAccount("Split-Bad-Sum");

        var ods = OdsTestBuilder.Build(("Transactions", new[]
        {
            new[] { "Date", "Kind", "Account", "Payee", "Category", "Amount", "Status", "Splits" },
            new[] { "2026-04-13", "Expense", account.Name, "Bad-Sum-Tx", "RT-Food", "100", "Cleared", "RT-Food=70;RT-Household=20" },
        }));

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(ods);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.oasis.opendocument.spreadsheet");
        form.Add(fileContent, "file", "splits-bad.ods");
        var importResp = await _api.Raw.PostAsync("/api/data/import/ods", form);
        importResp.EnsureSuccessStatusCode();
        var result = await importResp.Content.ReadFromJsonAsync<OdsImportResult>();

        Assert.Contains(result!.Warnings, w => w.Contains("Bad-Sum-Tx") && w.Contains("90"));
        // Transaction is still inserted, just without splits.
        var listResp = await _api.Raw.GetAsync("/api/finance/transactions/?q=Bad-Sum-Tx");
        var page = await listResp.Content.ReadFromJsonAsync<TransactionPage>();
        Assert.Single(page!.Items);
        Assert.Empty(page.Items[0].Splits);
    }

    private record TransactionPage(TransactionDto[] Items, string? NextCursor);

    async Task<AccountDto> SeedNamedAccount(string name)
    {
        // The accounts API doesn't dedupe by name, so multiple tests calling the
        // same seed leaves duplicates that break the import-side ToDictionary by
        // lowercased name. Look the account up first and reuse it if present.
        var existing = await GetAccountByName(name);
        if (existing is not null) return existing;
        var resp = await _api.PostAsync("/api/finance/accounts/", new
        {
            name,
            institution = (string?)null,
            type = "Checking",
            currency = "CHF",
            startingBalance = 1000m,
            balance = 1000m,
            color = "#14b8a6",
            notes = (string?)null,
            isArchived = false,
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AccountDto>())!;
    }

    async Task<AccountDto?> GetAccountByName(string name)
    {
        await _api.EnsureAuthedAsync();
        var resp = await _api.Raw.GetAsync("/api/finance/accounts/");
        if (!resp.IsSuccessStatusCode) return null;
        var accounts = await resp.Content.ReadFromJsonAsync<AccountDto[]>();
        return accounts?.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    async Task SeedOneAccount()
    {
        var existing = await GetAccountByName("RT-Test");
        if (existing is not null) return;
        var resp = await _api.PostAsync("/api/finance/accounts/", new
        {
            name = "RT-Test",
            institution = (string?)null,
            type = "Checking",
            currency = "CHF",
            startingBalance = 100m,
            balance = 100m,
            color = "#14b8a6",
            notes = (string?)null,
            isArchived = false,
        });
        resp.EnsureSuccessStatusCode();
        var account = await resp.Content.ReadFromJsonAsync<AccountDto>();
        Assert.Equal("RT-Test", account!.Name);
    }
}
