using System.Net;
using System.Net.Http.Json;

namespace LuminaVault.Tests;

/// `/api/health` is useful operationally, but it still exposes app/database state.
/// Keep it behind auth; Docker uses `/internal/health`, which is loopback-only.
public class HealthTests : IClassFixture<LuminaVaultFactory>
{
    private readonly ApiClient _api;

    public HealthTests(LuminaVaultFactory factory)
    {
        _api = new ApiClient(factory.CreateClient());
    }

    private record HealthBody(string Status, string Database);

    [Fact]
    public async Task Health_rejects_anonymous_requests()
    {
        var resp = await _api.Raw.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Health_returns_ok_with_db_ping_when_authenticated()
    {
        await _api.EnsureAuthedAsync();

        var resp = await _api.Raw.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<HealthBody>();
        Assert.Equal("ok", body!.Status);
        Assert.Equal("ok", body.Database);
    }
}
