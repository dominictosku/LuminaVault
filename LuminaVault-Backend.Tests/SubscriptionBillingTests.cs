using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace LuminaVault.Tests;

/// Verifies the monthly-cost math the dashboard, statistics, and subscription totals
/// all rely on. The Month/Year cases are the ones users care about — they're now
/// exact (no 30-day approximation), so a CHF 10/month subscription reports CHF 10/month
/// instead of the old CHF 10.15. The Day/Week cases still use the 30.4375-day average.
public class SubscriptionBillingTests : IClassFixture<LuminaVaultFactory>
{
    private readonly ApiClient _api;

    public SubscriptionBillingTests(LuminaVaultFactory factory)
    {
        _api = new ApiClient(factory.CreateClient());
    }

    private record AccountDto(int Id, string Name);

    private record SubscriptionDto(
        int Id, string Name,
        decimal Amount, string Currency,
        [property: JsonPropertyName("billingIntervalUnit")] string Unit,
        [property: JsonPropertyName("billingIntervalCount")] int Count,
        [property: JsonPropertyName("billingIntervalDays")] int Days,
        DateTime NextDueOn,
        [property: JsonPropertyName("monthlyAmount")] decimal MonthlyAmount);

    [Theory]
    // Calendar-aligned units divide the amount evenly — no drift from the 30-day approx.
    [InlineData("Month", 1, 17.90, 17.90)]
    [InlineData("Month", 3, 30.00, 10.00)]    // Quarterly CHF 30 → CHF 10/month
    [InlineData("Month", 6, 60.00, 10.00)]    // Half-yearly CHF 60 → CHF 10/month
    [InlineData("Year", 1, 120.00, 10.00)]    // Annual CHF 120 → CHF 10/month
    [InlineData("Year", 2, 240.00, 10.00)]    // Every 2 years CHF 240 → CHF 10/month
    // Day/week intervals use the 30.4375-day average month.
    [InlineData("Day", 30, 17.90, 18.16)]     // What 30-day-as-monthly looked like before
    [InlineData("Day", 7, 10.00, 43.48)]      // Weekly via days: 10 × 30.4375 / 7
    [InlineData("Week", 1, 10.00, 43.33)]     // Weekly via weeks: 10 × (52/12)
    [InlineData("Week", 2, 20.00, 43.33)]     // Bi-weekly CHF 20 → ~43.33/mo
    [InlineData("Day", 365, 100.00, 8.34)]    // Daily-365 falls within rounding of /12
    public async Task Monthly_amount_matches_unit_and_count(string unit, int count, decimal amount, decimal expectedMonthly)
    {
        await _api.EnsureAuthedAsync();
        var account = await SeedAccount($"Sub-{Guid.NewGuid():N}");
        var sub = await CreateSubscription(account.Id, unit, count, amount);
        Assert.Equal(expectedMonthly, sub.MonthlyAmount);
    }

    [Fact]
    public async Task Legacy_input_with_only_days_still_works()
    {
        // Old clients that don't know about Unit/Count send `billingIntervalDays`.
        // The endpoint should treat that as "every N days" and compute monthly cost
        // using the 30.4375-day average — matching pre-migration behavior exactly.
        await _api.EnsureAuthedAsync();
        var account = await SeedAccount($"Legacy-{Guid.NewGuid():N}");
        var resp = await _api.PostAsync("/api/finance/subscriptions/", new
        {
            name = "Legacy 30-day", category = "Subscriptions", provider = (string?)null,
            accountId = account.Id, amount = 17.90m, currency = "CHF",
            billingIntervalDays = 30,
            startedOn = "2026-01-01", nextDueOn = "2026-02-01",
            autoRenew = true, status = "Active", notes = (string?)null,
        });
        resp.EnsureSuccessStatusCode();
        var sub = (await resp.Content.ReadFromJsonAsync<SubscriptionDto>())!;
        Assert.Equal("Day", sub.Unit);
        Assert.Equal(30, sub.Count);
        Assert.Equal(18.16m, sub.MonthlyAmount);
    }

    [Fact]
    public async Task Generating_a_monthly_transaction_lands_on_the_same_calendar_day_next_month()
    {
        // The whole point of Month intervals is calendar-aware due dates. Jan 31 → Feb 28
        // (or 29 in a leap year), and Feb 28 + 1 month → Mar 28 — both via AddMonths(1).
        await _api.EnsureAuthedAsync();
        var account = await SeedAccount($"Cal-{Guid.NewGuid():N}");
        var sub = await CreateSubscription(account.Id, "Month", 1, 25m, nextDueOn: "2026-01-31");

        var gen = await _api.PostAsync($"/api/finance/subscriptions/{sub.Id}/generate-transaction", new
        {
            status = "Pending", advanceNextDueOn = true,
        });
        gen.EnsureSuccessStatusCode();
        var body = await gen.Content.ReadFromJsonAsync<GenerateResult>();
        Assert.NotNull(body);
        Assert.Equal(new DateTime(2026, 2, 28), body!.Subscription.NextDueOn.Date);
    }

    [Fact]
    public async Task Yearly_subscription_advances_by_one_year_not_365_days()
    {
        // 2028 is a leap year, so 365 days from Feb 29 2028 lands on Feb 28 2029,
        // not Feb 29 2029. AddYears(1) is the right semantic.
        await _api.EnsureAuthedAsync();
        var account = await SeedAccount($"Year-{Guid.NewGuid():N}");
        var sub = await CreateSubscription(account.Id, "Year", 1, 120m, nextDueOn: "2028-02-29");

        var gen = await _api.PostAsync($"/api/finance/subscriptions/{sub.Id}/generate-transaction", new
        {
            status = "Pending", advanceNextDueOn = true,
        });
        gen.EnsureSuccessStatusCode();
        var body = await gen.Content.ReadFromJsonAsync<GenerateResult>();
        Assert.Equal(new DateTime(2029, 2, 28), body!.Subscription.NextDueOn.Date);
    }

    private record GenerateResult(SubscriptionDto Subscription);

    async Task<AccountDto> SeedAccount(string name)
    {
        var resp = await _api.PostAsync("/api/finance/accounts/", new
        {
            name, institution = (string?)null, type = "Checking", currency = "CHF",
            startingBalance = 1000m, balance = 1000m, color = "#14b8a6",
            notes = (string?)null, isArchived = false,
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AccountDto>())!;
    }

    async Task<SubscriptionDto> CreateSubscription(int accountId, string unit, int count, decimal amount, string nextDueOn = "2026-02-01")
    {
        var resp = await _api.PostAsync("/api/finance/subscriptions/", new
        {
            name = $"Sub-{unit}-{count}-{amount}", category = "Subscriptions",
            provider = (string?)null, accountId, amount, currency = "CHF",
            billingIntervalUnit = unit, billingIntervalCount = count,
            startedOn = "2026-01-01", nextDueOn,
            autoRenew = true, status = "Active", notes = (string?)null,
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<SubscriptionDto>())!;
    }
}
