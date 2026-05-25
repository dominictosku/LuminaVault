using System.Net;
using System.Net.Http.Headers;
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
    public async Task Register_rejects_missing_password_without_500()
    {
        using var factory = new LuminaVaultFactory();
        var http = factory.CreateClient();

        var resp = await http.PostAsJsonAsync("/api/auth/register", new { username = "alice" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_rejects_request_without_token()
    {
        using var factory = new LuminaVaultFactory();
        var http = factory.CreateClient();
        var resp = await http.GetAsync("/api/finance/accounts");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/api/health")]
    [InlineData("/api/photos/1")]
    [InlineData("/api/attachments/1")]
    [InlineData("/api/items/1/model")]
    public async Task Non_auth_endpoints_reject_request_without_token(string path)
    {
        using var factory = new LuminaVaultFactory();
        var http = factory.CreateClient();

        var resp = await http.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Auth_status_remains_public_for_login_setup_flow()
    {
        using var factory = new LuminaVaultFactory();
        var http = factory.CreateClient();

        var resp = await http.GetAsync("/api/auth/status");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Change_password_rotates_password_and_returns_new_token()
    {
        using var factory = new LuminaVaultFactory();
        var http = factory.CreateClient();
        var register = await http.PostAsJsonAsync("/api/auth/register",
            new { username = "changer", password = "password123" });
        register.EnsureSuccessStatusCode();
        var firstToken = await register.Content.ReadFromJsonAsync<ApiClient.AuthResponse>();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstToken!.Token);

        var change = await http.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "password123", newPassword = "better-password" });
        change.EnsureSuccessStatusCode();
        var changed = await change.Content.ReadFromJsonAsync<ApiClient.AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(changed!.Token));

        http.DefaultRequestHeaders.Authorization = null;
        var oldLogin = await http.PostAsJsonAsync("/api/auth/login",
            new { username = "changer", password = "password123" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await http.PostAsJsonAsync("/api/auth/login",
            new { username = "changer", password = "better-password" });
        newLogin.EnsureSuccessStatusCode();
    }
}
