using System.Net;
using System.Net.Http.Json;

namespace LuminaVault.Tests;

/// The login endpoint is the password-guessing surface, so it gets a tighter per-IP window
/// than the rest of the API. Fresh factory (own in-memory limiter) and a single test method
/// so the partition isn't shared with other login calls.
public class RateLimitTests
{
    [Fact]
    public async Task Login_throttles_after_the_per_window_limit()
    {
        const int permitLimit = 10;
        using var factory = new LuminaVaultFactory("Development", new Dictionary<string, string?>
        {
            ["RateLimiting:LoginPermitLimit"] = permitLimit.ToString(),
        });
        var http = factory.CreateClient();
        var wrongCredentials = new { username = "nobody", password = "wrong-password" };

        // The configured allowance reaches the handler and comes back 401 (no such user).
        for (var attempt = 1; attempt <= permitLimit; attempt++)
        {
            var resp = await http.PostAsJsonAsync("/api/auth/login", wrongCredentials);
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        // The next attempt in the same window is rejected by the limiter before the handler.
        var throttled = await http.PostAsJsonAsync("/api/auth/login", wrongCredentials);
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }
}
