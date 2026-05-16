using System.Net.Http.Json;

namespace LuminaVault.Tests;

/// Covers the three scanner rules + the Source-keyed dedup invariant.
/// Tests trigger scans via POST /api/notifications/scan rather than waiting for the
/// background tick, which would make the suite take 6h to run.
public class NotificationsTests : IClassFixture<LuminaVaultFactory>
{
    private readonly ApiClient _api;

    public NotificationsTests(LuminaVaultFactory factory)
    {
        _api = new ApiClient(factory.CreateClient());
    }

    private record AccountDto(int Id, string Name);
    private record NotificationDto(int Id, string Type, string Severity, string Title, string Message, string? Link, string Source, string Status);
    private record CountsDto(int Unread, int Total);
    private record ScanResultDto(int Touched, DateTime ScannedAt);

    private async Task<AccountDto> CreateAccount(string name, decimal balance, string currency = "CHF", string type = "Checking")
    {
        var resp = await _api.PostAsync("/api/finance/accounts/", new
        {
            name, institution = (string?)null, type, currency,
            startingBalance = balance, balance,
            color = "#14b8a6", notes = (string?)null, isArchived = false,
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AccountDto>())!;
    }

    [Fact]
    public async Task Scan_fires_subscription_due_alert_within_lookahead_window()
    {
        var account = await CreateAccount("Sub account", balance: 1000m);
        var dueDate = DateTime.UtcNow.Date.AddDays(2);
        await _api.PostAsync("/api/finance/subscriptions/", new
        {
            name = "Hosting", category = "Subscriptions", provider = (string?)null,
            accountId = account.Id, amount = 19.99m, currency = "CHF",
            billingIntervalDays = 30, startedOn = dueDate.AddDays(-30),
            nextDueOn = dueDate, autoRenew = true, status = "Active", notes = (string?)null,
        });

        var scan = await _api.PostAsync("/api/notifications/scan", new { });
        scan.EnsureSuccessStatusCode();

        var items = await _api.GetAsync<NotificationDto[]>("/api/notifications/");
        // Tests share the SQLite fixture — narrow to this test's subscription by name.
        var alert = Assert.Single(items!, n => n.Type == "SubscriptionDue" && n.Title.Contains("Hosting"));
        Assert.Equal("Info", alert.Severity);
        Assert.Equal("/subscriptions", alert.Link);
        Assert.Equal("Unread", alert.Status);
    }

    [Fact]
    public async Task Rescan_does_not_duplicate_existing_alerts()
    {
        var account = await CreateAccount("Sub dedup", balance: 500m);
        await _api.PostAsync("/api/finance/subscriptions/", new
        {
            name = "Newspaper", category = "Subscriptions", provider = (string?)null,
            accountId = account.Id, amount = 10m, currency = "CHF",
            billingIntervalDays = 30, startedOn = DateTime.UtcNow.Date,
            nextDueOn = DateTime.UtcNow.Date.AddDays(1),
            autoRenew = true, status = "Active", notes = (string?)null,
        });

        for (var i = 0; i < 3; i++)
        {
            var resp = await _api.PostAsync("/api/notifications/scan", new { });
            resp.EnsureSuccessStatusCode();
        }

        var items = await _api.GetAsync<NotificationDto[]>("/api/notifications/");
        var newspaperAlerts = items!.Where(n => n.Type == "SubscriptionDue" && n.Title.Contains("Newspaper")).ToList();
        Assert.Single(newspaperAlerts);
    }

    [Fact]
    public async Task Scan_fires_budget_overrun_when_spent_exceeds_limit()
    {
        var account = await CreateAccount("Budget account", balance: 1000m);
        var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        await _api.PostAsync("/api/finance/budgets/", new
        {
            category = "Food", month, limitAmount = 50m, notes = (string?)null,
        });
        await _api.PostAsync("/api/finance/transactions/", new
        {
            accountId = account.Id, transferAccountId = (int?)null,
            kind = "Expense", status = "Cleared", occurredOn = DateTime.UtcNow.Date,
            payee = "Grocery", category = "Food", amount = 80m,
            description = (string?)null, notes = (string?)null,
            tags = Array.Empty<string>(),
        });

        var scan = await _api.PostAsync("/api/notifications/scan", new { });
        scan.EnsureSuccessStatusCode();

        var items = await _api.GetAsync<NotificationDto[]>("/api/notifications/");
        var alert = Assert.Single(items!, n => n.Type == "BudgetOverrun");
        Assert.Equal("Warning", alert.Severity);
        Assert.Equal("/budgets", alert.Link);
        Assert.Contains("Food", alert.Title);
    }

    [Fact]
    public async Task Dismissed_alert_is_resurfaced_when_condition_still_holds()
    {
        var account = await CreateAccount("Resurface", balance: 200m);
        await _api.PostAsync("/api/finance/subscriptions/", new
        {
            name = "Resurfaced sub", category = "Subscriptions", provider = (string?)null,
            accountId = account.Id, amount = 5m, currency = "CHF",
            billingIntervalDays = 30, startedOn = DateTime.UtcNow.Date,
            nextDueOn = DateTime.UtcNow.Date.AddDays(1),
            autoRenew = true, status = "Active", notes = (string?)null,
        });
        await _api.PostAsync("/api/notifications/scan", new { });

        var items = await _api.GetAsync<NotificationDto[]>("/api/notifications/");
        var alert = items!.Single(n => n.Type == "SubscriptionDue" && n.Title.Contains("Resurfaced sub"));
        var dismiss = await _api.PostAsync($"/api/notifications/{alert.Id}/dismiss", new { });
        dismiss.EnsureSuccessStatusCode();

        // Re-scan: same Source, condition still holds → existing row flips back to Unread.
        await _api.PostAsync("/api/notifications/scan", new { });

        var listIncludingDismissed = await _api.GetAsync<NotificationDto[]>("/api/notifications/?status=Unread");
        Assert.Contains(listIncludingDismissed!, n => n.Id == alert.Id && n.Status == "Unread");
    }

    [Fact]
    public async Task Counts_endpoint_excludes_dismissed_from_unread_and_total()
    {
        var account = await CreateAccount("Counts", balance: 200m);
        await _api.PostAsync("/api/finance/subscriptions/", new
        {
            name = "Counts sub", category = "Subscriptions", provider = (string?)null,
            accountId = account.Id, amount = 5m, currency = "CHF",
            billingIntervalDays = 30, startedOn = DateTime.UtcNow.Date,
            nextDueOn = DateTime.UtcNow.Date.AddDays(1),
            autoRenew = true, status = "Active", notes = (string?)null,
        });
        await _api.PostAsync("/api/notifications/scan", new { });

        var before = await _api.GetAsync<CountsDto>("/api/notifications/counts");
        Assert.True(before!.Unread >= 1);

        var items = await _api.GetAsync<NotificationDto[]>("/api/notifications/");
        var alert = items!.Single(n => n.Title.Contains("Counts sub"));
        await _api.PostAsync($"/api/notifications/{alert.Id}/dismiss", new { });

        var after = await _api.GetAsync<CountsDto>("/api/notifications/counts");
        Assert.Equal(before.Unread - 1, after!.Unread);
        Assert.Equal(before.Total - 1, after.Total);
    }
}
