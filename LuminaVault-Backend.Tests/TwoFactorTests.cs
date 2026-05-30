using System.Net;
using System.Net.Http.Json;
using LuminaVault.Auth;

namespace LuminaVault.Tests;

/// End-to-end TOTP enrolment and the login challenge. Each test gets its own DB so enabling
/// 2FA for the single user doesn't leak across tests.
public class TwoFactorTests
{
    private const string User = "tester";
    private const string Password = "password123";

    private record SetupResponse(string Secret, string OtpauthUri);
    private record EnableResponse(string[] RecoveryCodes);
    private record StatusResponse(bool Enabled, int RecoveryCodesRemaining);
    private record TokenResponse(string Token, string Username);

    private static string CurrentCode(string secret)
    {
        var totp = new TotpService();
        return totp.ComputeCode(TotpService.Base32Decode(secret), DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
    }

    /// Registers + authenticates the single user, then walks setup/enable so the caller starts
    /// with 2FA active. Returns the authed client, the TOTP secret, and the recovery codes.
    private static async Task<(HttpClient Http, string Secret, string[] Recovery)> EnableTwoFactor(LuminaVaultFactory factory)
    {
        var api = new ApiClient(factory.CreateClient());
        await api.EnsureAuthedAsync(User, Password);

        var setup = await (await api.Raw.PostAsync("/api/auth/2fa/setup", null)).Content.ReadFromJsonAsync<SetupResponse>();
        var enableResp = await api.Raw.PostAsJsonAsync("/api/auth/2fa/enable", new { code = CurrentCode(setup!.Secret) });
        enableResp.EnsureSuccessStatusCode();
        var enable = await enableResp.Content.ReadFromJsonAsync<EnableResponse>();

        return (api.Raw, setup.Secret, enable!.RecoveryCodes);
    }

    [Fact]
    public async Task Setup_returns_a_secret_and_otpauth_uri_without_enabling_yet()
    {
        using var factory = new LuminaVaultFactory();
        var api = new ApiClient(factory.CreateClient());
        await api.EnsureAuthedAsync(User, Password);

        var setup = await (await api.Raw.PostAsync("/api/auth/2fa/setup", null)).Content.ReadFromJsonAsync<SetupResponse>();
        Assert.False(string.IsNullOrWhiteSpace(setup!.Secret));
        Assert.Contains($"secret={setup.Secret}", setup.OtpauthUri);

        var status = await api.Raw.GetFromJsonAsync<StatusResponse>("/api/auth/2fa/status");
        Assert.False(status!.Enabled); // not active until a code is confirmed
    }

    [Fact]
    public async Task Enable_with_a_wrong_code_is_rejected()
    {
        using var factory = new LuminaVaultFactory();
        var api = new ApiClient(factory.CreateClient());
        await api.EnsureAuthedAsync(User, Password);
        await api.Raw.PostAsync("/api/auth/2fa/setup", null);

        var resp = await api.Raw.PostAsJsonAsync("/api/auth/2fa/enable", new { code = "000000" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var status = await api.Raw.GetFromJsonAsync<StatusResponse>("/api/auth/2fa/status");
        Assert.False(status!.Enabled);
    }

    [Fact]
    public async Task Enable_with_the_right_code_turns_on_2fa_and_issues_recovery_codes()
    {
        using var factory = new LuminaVaultFactory();
        var (http, _, recovery) = await EnableTwoFactor(factory);

        Assert.Equal(10, recovery.Length);
        var status = await http.GetFromJsonAsync<StatusResponse>("/api/auth/2fa/status");
        Assert.True(status!.Enabled);
        Assert.Equal(10, status.RecoveryCodesRemaining);
    }

    [Fact]
    public async Task Login_with_only_a_password_is_challenged_when_2fa_is_on()
    {
        using var factory = new LuminaVaultFactory();
        await EnableTwoFactor(factory);

        var resp = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = User, password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.True(body!.ContainsKey("twoFactorRequired"));
    }

    [Fact]
    public async Task Login_succeeds_with_password_plus_a_valid_totp_code()
    {
        using var factory = new LuminaVaultFactory();
        var (_, secret, _) = await EnableTwoFactor(factory);

        var resp = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { username = User, password = Password, totpCode = CurrentCode(secret) });

        resp.EnsureSuccessStatusCode();
        var token = await resp.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.False(string.IsNullOrWhiteSpace(token!.Token));
    }

    [Fact]
    public async Task Login_with_a_wrong_totp_code_is_rejected()
    {
        using var factory = new LuminaVaultFactory();
        await EnableTwoFactor(factory);

        var resp = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { username = User, password = Password, totpCode = "000000" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task A_recovery_code_logs_in_once_and_is_then_consumed()
    {
        using var factory = new LuminaVaultFactory();
        var (http, _, recovery) = await EnableTwoFactor(factory);
        var code = recovery[0];

        var first = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { username = User, password = Password, recoveryCode = code });
        first.EnsureSuccessStatusCode();

        var status = await http.GetFromJsonAsync<StatusResponse>("/api/auth/2fa/status");
        Assert.Equal(9, status!.RecoveryCodesRemaining);

        var reuse = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { username = User, password = Password, recoveryCode = code });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task Disable_requires_the_password_and_restores_single_factor_login()
    {
        using var factory = new LuminaVaultFactory();
        var (http, _, _) = await EnableTwoFactor(factory);

        var wrong = await http.PostAsJsonAsync("/api/auth/2fa/disable", new { password = "not-the-password" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var ok = await http.PostAsJsonAsync("/api/auth/2fa/disable", new { password = Password });
        ok.EnsureSuccessStatusCode();

        // Password alone works again.
        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = User, password = Password });
        login.EnsureSuccessStatusCode();
    }
}
