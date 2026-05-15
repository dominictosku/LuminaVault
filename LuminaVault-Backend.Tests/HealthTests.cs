using System.Net;
using System.Net.Http.Json;

namespace LuminaVault.Tests;

/// `/api/health` is the hook reverse proxies, uptime monitors, and docker healthcheck
/// rely on. Pin its wire shape so a well-meaning refactor doesn't quietly break
/// monitoring.
public class HealthTests : IClassFixture<LuminaVaultFactory>
{
    private readonly HttpClient _http;

    public HealthTests(LuminaVaultFactory factory)
    {
        _http = factory.CreateClient();
    }

    private record HealthBody(string Status, string Database);

    [Fact]
    public async Task Health_is_anonymous_and_returns_ok_with_db_ping()
    {
        // No auth header attached — anonymous access is intentional so dumb monitors work.
        var resp = await _http.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<HealthBody>();
        Assert.Equal("ok", body!.Status);
        Assert.Equal("ok", body.Database);
    }
}
