using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LuminaVault.Tests;

public class BankCsvImportTests : IClassFixture<LuminaVaultFactory>
{
    private readonly ApiClient _api;

    public BankCsvImportTests(LuminaVaultFactory factory)
    {
        _api = new ApiClient(factory.CreateClient());
    }

    private record AccountDto(int Id, string Name, decimal Balance);

    private record CsvImportResult(
        [property: JsonPropertyName("transactions")] int Transactions,
        [property: JsonPropertyName("duplicates")] int Duplicates,
        [property: JsonPropertyName("skipped")] int Skipped,
        [property: JsonPropertyName("warnings")] string[] Warnings);

    private record CsvPreviewResult(
        [property: JsonPropertyName("headers")] string[] Headers,
        [property: JsonPropertyName("sampleRows")] string[][] SampleRows,
        [property: JsonPropertyName("suggestedColumns")] Dictionary<string, string> SuggestedColumns);

    private record TransactionDto(
        int Id,
        int AccountId,
        string Kind,
        string Payee,
        string Category,
        decimal Amount);

    [Fact]
    public async Task Bank_csv_import_creates_transactions_and_updates_balance()
    {
        var account = await CreateAccount("CSV Main", 100m);
        var csv = """
Date;Payee;Amount;Category;Description
2026-05-01;Migros;-42.50;Groceries;Weekly shop
2026-05-02;Salary;2500.00;Salary;May salary
""";

        var result = await Import(csv, account.Id);

        Assert.Equal(2, result.Transactions);
        Assert.Equal(0, result.Duplicates);
        Assert.Equal(0, result.Skipped);

        var accounts = await _api.GetAsync<AccountDto[]>("/api/finance/accounts/");
        Assert.Equal(2557.50m, accounts!.Single(a => a.Id == account.Id).Balance);

        var transactions = await _api.GetAsync<TransactionDto[]>($"/api/finance/transactions?accountId={account.Id}");
        Assert.Contains(transactions!, t => t.Payee == "Migros" && t.Kind == "Expense" && t.Amount == 42.50m);
        Assert.Contains(transactions!, t => t.Payee == "Salary" && t.Kind == "Income" && t.Amount == 2500m);
    }

    [Fact]
    public async Task Bank_csv_preview_returns_headers_samples_and_suggested_mapping()
    {
        var csv = """
Date;Payee;Amount;Category
2026-05-01;Migros;-42.50;Groceries
""";

        await _api.EnsureAuthedAsync();
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(fileContent, "file", "bank.csv");

        var resp = await _api.Raw.PostAsync("/api/data/import/bank-csv/preview", form);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<CsvPreviewResult>();

        Assert.Equal(["Date", "Payee", "Amount", "Category"], result!.Headers);
        Assert.Single(result.SampleRows);
        Assert.Equal("Amount", result.SuggestedColumns["Amount"]);
    }

    [Fact]
    public async Task Bank_csv_import_skips_likely_duplicates_on_second_import()
    {
        var account = await CreateAccount("CSV Dedupe", 100m);
        var csv = """
Date;Payee;Amount;Category
2026-05-03;Coop;-15.20;Groceries
""";

        var first = await Import(csv, account.Id);
        var second = await Import(csv, account.Id);

        Assert.Equal(1, first.Transactions);
        Assert.Equal(0, second.Transactions);
        Assert.Equal(1, second.Duplicates);
    }

    [Fact]
    public async Task Bank_csv_import_supports_debit_and_credit_columns()
    {
        var account = await CreateAccount("CSV Split", 100m);
        var csv = """
Datum;Gegenpartei;Belastung;Gutschrift;Kategorie
04.05.2026;Rent;1200,00;;Housing
05.05.2026;Refund;;20,00;Refunds
""";

        var result = await Import(csv, account.Id, new Dictionary<string, string>
        {
            ["Date"] = "Datum",
            ["Payee"] = "Gegenpartei",
            ["Amount"] = "",
            ["Debit"] = "Belastung",
            ["Credit"] = "Gutschrift",
            ["Category"] = "Kategorie",
            ["Description"] = "",
            ["Notes"] = "",
            ["Tags"] = "",
            ["Status"] = "",
        });

        Assert.Equal(2, result.Transactions);
        var accounts = await _api.GetAsync<AccountDto[]>("/api/finance/accounts/");
        Assert.Equal(-1080m, accounts!.Single(a => a.Id == account.Id).Balance);
    }

    private async Task<AccountDto> CreateAccount(string name, decimal balance)
    {
        var resp = await _api.PostAsync("/api/finance/accounts/", new
        {
            name,
            institution = (string?)null,
            type = "Checking",
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

    private async Task<CsvImportResult> Import(
        string csv,
        int accountId,
        Dictionary<string, string>? columns = null)
    {
        await _api.EnsureAuthedAsync();
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(fileContent, "file", "bank.csv");
        form.Add(new StringContent(JsonSerializer.Serialize(new
        {
            accountId,
            columns = columns ?? new Dictionary<string, string>
            {
                ["Date"] = "Date",
                ["Payee"] = "Payee",
                ["Amount"] = "Amount",
                ["Debit"] = "",
                ["Credit"] = "",
                ["Category"] = "Category",
                ["Description"] = "Description",
                ["Notes"] = "",
                ["Tags"] = "",
                ["Status"] = "",
            },
            defaultCategory = "Imported",
            status = "Cleared",
        })), "mappingJson");

        var resp = await _api.Raw.PostAsync("/api/data/import/bank-csv", form);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<CsvImportResult>())!;
    }
}
