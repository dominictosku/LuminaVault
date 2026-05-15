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

    async Task SeedOneAccount()
    {
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
