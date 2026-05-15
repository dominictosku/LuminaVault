using System.Net;
using System.Net.Http.Json;

namespace LuminaVault.Tests;

/// Auth tests register different usernames, so each test gets a fresh DB
/// (no IClassFixture — factory created per test and disposed at the end).
public class AuthTests
{
    [Fact]
    public async Task Register_returns_token()
    {
        using var factory = new LuminaVaultFactory();
        var http = factory.CreateClient();
        var resp = await http.PostAsJsonAsync("/api/auth/register",
            new { username = "alice", password = "password123" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ApiClient.AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal("alice", body.Username);
    }

    [Fact]
    public async Task Register_second_user_returns_conflict()
    {
        using var factory = new LuminaVaultFactory();
        var http = factory.CreateClient();
        var first = await http.PostAsJsonAsync("/api/auth/register",
            new { username = "first", password = "password123" });
        first.EnsureSuccessStatusCode();
        var second = await http.PostAsJsonAsync("/api/auth/register",
            new { username = "second", password = "password123" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_rejects_request_without_token()
    {
        using var factory = new LuminaVaultFactory();
        var http = factory.CreateClient();
        var resp = await http.GetAsync("/api/finance/accounts");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
