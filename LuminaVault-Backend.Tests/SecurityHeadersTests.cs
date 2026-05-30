using System.Net;

namespace LuminaVault.Tests;

/// Hardening headers must ride on every backend response — including unauthenticated and
/// error responses — so a content-sniffed payload (e.g. an inline item photo) can never be
/// coerced into executing as a document, and the API can never be framed.
public class SecurityHeadersTests : IClassFixture<LuminaVaultFactory>
{
    private readonly ApiClient _api;

    public SecurityHeadersTests(LuminaVaultFactory factory)
    {
        _api = new ApiClient(factory.CreateClient());
    }

    [Fact]
    public async Task Authenticated_responses_carry_hardening_headers()
    {
        await _api.EnsureAuthedAsync();

        var resp = await _api.Raw.GetAsync("/api/health");

        Assert.Equal("nosniff", Header(resp, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(resp, "X-Frame-Options"));
        Assert.Equal("no-referrer", Header(resp, "Referrer-Policy"));
        Assert.Contains("default-src 'none'", Header(resp, "Content-Security-Policy"));
    }

    [Fact]
    public async Task Headers_are_present_on_unauthenticated_responses()
    {
        // No token attached: the request is rejected at the auth layer, but the headers
        // are applied via OnStarting upstream of it, so they must still be on the 401.
        var resp = await _api.Raw.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        Assert.Equal("nosniff", Header(resp, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(resp, "X-Frame-Options"));
    }

    private static string Header(HttpResponseMessage resp, string name)
    {
        if (resp.Headers.TryGetValues(name, out var values))
            return string.Join(",", values);
        if (resp.Content.Headers.TryGetValues(name, out var contentValues))
            return string.Join(",", contentValues);
        return "";
    }
}
