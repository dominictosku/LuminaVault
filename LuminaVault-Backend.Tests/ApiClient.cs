using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace LuminaVault.Tests;

/// Thin wrapper around HttpClient that registers a user on first use and attaches the
/// JWT to every request. Keeps every test focused on the API call under test.
public class ApiClient
{
    private readonly HttpClient _http;
    private bool _authed;

    public ApiClient(HttpClient http) => _http = http;

    public async Task EnsureAuthedAsync(string username = "tester", string password = "password123")
    {
        if (_authed) return;
        // LuminaVault is single-user. Across multiple tests sharing the same DB fixture,
        // only the first register succeeds; the rest fall through to login.
        var register = await _http.PostAsJsonAsync("/api/auth/register", new { username, password });
        AuthResponse? token;
        if (register.IsSuccessStatusCode)
        {
            token = await register.Content.ReadFromJsonAsync<AuthResponse>();
        }
        else
        {
            var login = await _http.PostAsJsonAsync("/api/auth/login", new { username, password });
            login.EnsureSuccessStatusCode();
            token = await login.Content.ReadFromJsonAsync<AuthResponse>();
        }
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.Token);
        _authed = true;
    }

    public HttpClient Raw => _http;

    public async Task<T?> GetAsync<T>(string url)
    {
        await EnsureAuthedAsync();
        return await _http.GetFromJsonAsync<T>(url);
    }

    public async Task<HttpResponseMessage> PostAsync<T>(string url, T body)
    {
        await EnsureAuthedAsync();
        return await _http.PostAsJsonAsync(url, body);
    }

    public async Task<HttpResponseMessage> PutAsync<T>(string url, T body)
    {
        await EnsureAuthedAsync();
        return await _http.PutAsJsonAsync(url, body);
    }

    public async Task<HttpResponseMessage> DeleteAsync(string url)
    {
        await EnsureAuthedAsync();
        return await _http.DeleteAsync(url);
    }

    public record AuthResponse(string Token, string Username);
}
