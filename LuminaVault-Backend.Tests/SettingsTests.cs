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
}
