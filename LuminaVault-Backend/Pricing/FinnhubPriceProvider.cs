using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LuminaVault.Domain;

namespace LuminaVault.Pricing;

public class FinnhubPriceProvider : IPriceProvider
{
    public const string ProviderName = "Finnhub";
    private readonly HttpClient _http;
    private readonly ILogger<FinnhubPriceProvider> _log;
    private readonly string? _apiKey;

    public FinnhubPriceProvider(HttpClient http, IConfiguration config, ILogger<FinnhubPriceProvider> log)
    {
        _http = http;
        _log = log;
        _apiKey = config["PriceProviders:Finnhub:ApiKey"];
        if (string.IsNullOrWhiteSpace(_apiKey))
            _apiKey = Environment.GetEnvironmentVariable("LUMINA_FINNHUB_KEY");
    }

    public string Name => ProviderName;
    public bool Supports(FinanceAccountType accountType) => accountType == FinanceAccountType.Investment;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<IReadOnlyList<PriceQuote>> GetQuotesAsync(
        IReadOnlyCollection<PriceLookup> lookups,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return lookups.Select(l => new PriceQuote(l.HoldingId, null,
                "Finnhub API key not configured. Set PriceProviders:Finnhub:ApiKey in appsettings.json or LUMINA_FINNHUB_KEY env var.",
                null)).ToList();
        }

        // Finnhub free tier has no batch /quote endpoint, but allows ~60 calls/min.
        // Personal portfolios typically hold <30 symbols, so parallelism is safe.
        var tasks = lookups.Select(l => GetSingleQuoteAsync(l, ct));
        var results = await Task.WhenAll(tasks);
        return results;
    }

    async Task<PriceQuote> GetSingleQuoteAsync(PriceLookup lookup, CancellationToken ct)
    {
        var symbol = !string.IsNullOrWhiteSpace(lookup.ProviderId)
            ? lookup.ProviderId.Trim()
            : lookup.Symbol.Trim();
        var url = $"quote?symbol={Uri.EscapeDataString(symbol)}&token={Uri.EscapeDataString(_apiKey!)}";
        try
        {
            using var resp = await _http.GetAsync(url, ct);
            if ((int)resp.StatusCode == 429)
                return new PriceQuote(lookup.HoldingId, null, "Finnhub rate limit hit (60 calls/min on free tier). Try again shortly.", null);
            resp.EnsureSuccessStatusCode();
            var data = await resp.Content.ReadFromJsonAsync<QuoteResponse>(cancellationToken: ct);
            if (data is null || !data.CurrentPrice.HasValue || data.CurrentPrice.Value <= 0)
            {
                return new PriceQuote(lookup.HoldingId, null,
                    $"Finnhub returned no price for '{symbol}'. Set Provider ID with the exchange suffix if needed (e.g. NESN.SW for Swiss, SAP.DE for German).",
                    null);
            }
            return new PriceQuote(lookup.HoldingId, data.CurrentPrice, null, symbol);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Finnhub quote failed for {Symbol}", symbol);
            return new PriceQuote(lookup.HoldingId, null, $"Finnhub request failed: {ex.Message}", null);
        }
    }

    private record QuoteResponse(
        [property: JsonPropertyName("c")] decimal? CurrentPrice,
        [property: JsonPropertyName("pc")] decimal? PreviousClose,
        [property: JsonPropertyName("t")] long? Timestamp);
}
