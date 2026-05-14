using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LuminaVault.Domain;

namespace LuminaVault.Pricing;

public class CoinGeckoPriceProvider : IPriceProvider
{
    public const string ProviderName = "CoinGecko";
    private readonly HttpClient _http;
    private readonly ILogger<CoinGeckoPriceProvider> _log;

    public CoinGeckoPriceProvider(HttpClient http, ILogger<CoinGeckoPriceProvider> log)
    {
        _http = http;
        _log = log;
    }

    public string Name => ProviderName;
    public bool Supports(FinanceAccountType accountType) => accountType == FinanceAccountType.Crypto;

    public async Task<IReadOnlyList<PriceQuote>> GetQuotesAsync(
        IReadOnlyCollection<PriceLookup> lookups,
        CancellationToken ct = default)
    {
        var quotes = new List<PriceQuote>(lookups.Count);

        foreach (var byCurrency in lookups.GroupBy(l => (l.Currency ?? "usd").ToLowerInvariant()))
        {
            var currency = byCurrency.Key;
            var withIds = byCurrency.Where(l => !string.IsNullOrWhiteSpace(l.ProviderId)).ToList();
            var withoutIds = byCurrency.Where(l => string.IsNullOrWhiteSpace(l.ProviderId)).ToList();

            if (withIds.Count > 0)
                quotes.AddRange(await FetchByIdsAsync(withIds, currency, ct));

            if (withoutIds.Count > 0)
                quotes.AddRange(await FetchByTickersAsync(withoutIds, currency, ct));
        }

        return quotes;
    }

    async Task<IReadOnlyList<PriceQuote>> FetchByIdsAsync(
        IReadOnlyList<PriceLookup> lookups, string currency, CancellationToken ct)
    {
        var ids = string.Join(",", lookups.Select(l => l.ProviderId!.ToLowerInvariant()).Distinct());
        var url = $"simple/price?ids={Uri.EscapeDataString(ids)}&vs_currencies={Uri.EscapeDataString(currency)}";
        try
        {
            using var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            var prices = await resp.Content.ReadFromJsonAsync<Dictionary<string, Dictionary<string, decimal>>>(cancellationToken: ct)
                         ?? new Dictionary<string, Dictionary<string, decimal>>();
            return lookups.Select(l =>
            {
                var key = l.ProviderId!.ToLowerInvariant();
                if (prices.TryGetValue(key, out var byCur) && byCur.TryGetValue(currency, out var price))
                    return new PriceQuote(l.HoldingId, price, null, l.ProviderId);
                return new PriceQuote(l.HoldingId, null,
                    $"CoinGecko returned no price for '{l.ProviderId}' in {currency.ToUpperInvariant()}", l.ProviderId);
            }).ToList();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "CoinGecko id lookup failed for {Ids}", ids);
            return lookups.Select(l => new PriceQuote(l.HoldingId, null, $"CoinGecko request failed: {ex.Message}", l.ProviderId)).ToList();
        }
    }

    async Task<IReadOnlyList<PriceQuote>> FetchByTickersAsync(
        IReadOnlyList<PriceLookup> lookups, string currency, CancellationToken ct)
    {
        var symbols = string.Join(",", lookups.Select(l => l.Symbol.ToLowerInvariant()).Distinct());
        var url = $"coins/markets?vs_currency={Uri.EscapeDataString(currency)}&symbols={Uri.EscapeDataString(symbols)}";
        try
        {
            using var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            var entries = await resp.Content.ReadFromJsonAsync<List<MarketEntry>>(cancellationToken: ct)
                          ?? new List<MarketEntry>();

            // Multiple coins can share a ticker (forks). Prefer the one with the highest market cap.
            var bySymbol = entries
                .Where(e => !string.IsNullOrEmpty(e.Symbol))
                .GroupBy(e => e.Symbol!.ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.MarketCap ?? 0m).First());

            return lookups.Select(l =>
            {
                if (bySymbol.TryGetValue(l.Symbol.ToUpperInvariant(), out var match) && match.CurrentPrice.HasValue)
                    return new PriceQuote(l.HoldingId, match.CurrentPrice, null, match.Id);
                return new PriceQuote(l.HoldingId, null,
                    $"CoinGecko has no '{l.Symbol}' priced in {currency.ToUpperInvariant()} — set Provider ID for an exact match",
                    null);
            }).ToList();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "CoinGecko ticker lookup failed for {Symbols}", symbols);
            return lookups.Select(l => new PriceQuote(l.HoldingId, null, $"CoinGecko request failed: {ex.Message}", null)).ToList();
        }
    }

    private record MarketEntry(
        string? Id,
        string? Symbol,
        [property: JsonPropertyName("current_price")] decimal? CurrentPrice,
        [property: JsonPropertyName("market_cap")] decimal? MarketCap);
}
