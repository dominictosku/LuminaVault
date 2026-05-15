using System.Net;
using System.Net.Http.Json;

namespace LuminaVault.Tests;

/// Pins down the error wire shape (`{ error: "msg" }`) that the Angular client
/// reads via `e?.error?.error` in 10+ pages. Don't switch to ProblemDetails
/// without coordinating with the frontend.
public class ValidationTests : IClassFixture<LuminaVaultFactory>
{
    private readonly ApiClient _api;

    public ValidationTests(LuminaVaultFactory factory)
    {
        _api = new ApiClient(factory.CreateClient());
    }

    private record ErrorBody(string? Error);

    private record AccountDto(int Id);

    private async Task<AccountDto> CreateAccount()
    {
        var resp = await _api.PostAsync("/api/finance/accounts/", new
        {
            name = "Main",
            type = "Checking",
            currency = "CHF",
            startingBalance = 100m,
            balance = 100m,
            color = "#14b8a6",
            isArchived = false,
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AccountDto>())!;
    }

    [Fact]
    public async Task Account_create_without_name_returns_error_envelope()
    {
        var resp = await _api.PostAsync("/api/finance/accounts/", new
        {
            name = "",
            type = "Checking",
            currency = "CHF",
            startingBalance = 0m,
            balance = 0m,
            color = "#14b8a6",
            isArchived = false,
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Error));
        Assert.Contains("required", body.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Transaction_with_zero_amount_is_rejected_with_envelope()
    {
        var account = await CreateAccount();
        var resp = await _api.PostAsync("/api/finance/transactions/", new
        {
            accountId = account.Id,
            transferAccountId = (int?)null,
            kind = "Expense",
            status = "Cleared",
            occurredOn = DateTime.UtcNow.Date,
            payee = "X",
            category = "Food",
            amount = 0m,
            description = "",
            notes = "",
            tags = Array.Empty<string>(),
            symbol = (string?)null,
            quantity = (decimal?)null,
            pricePerUnit = (decimal?)null,
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Contains("greater than zero", body!.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Transfer_without_destination_is_rejected()
    {
        var account = await CreateAccount();
        var resp = await _api.PostAsync("/api/finance/transactions/", new
        {
            accountId = account.Id,
            transferAccountId = (int?)null,
            kind = "Transfer",
            status = "Cleared",
            occurredOn = DateTime.UtcNow.Date,
            payee = "Move",
            category = "Transfer",
            amount = 10m,
            description = "",
            notes = "",
            tags = Array.Empty<string>(),
            symbol = (string?)null,
            quantity = (decimal?)null,
            pricePerUnit = (decimal?)null,
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.NotNull(body!.Error);
    }
}
