using LuminaVault.Data;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

internal static class CurrencyConversion
{
    public const string BaseCurrency = "CHF";

    public static async Task<Dictionary<string, decimal>> LoadRates(AppDbContext db)
    {
        var rates = await db.ExchangeRates.ToDictionaryAsync(
            r => Normalize(r.Currency),
            r => r.RateToBase);
        rates[BaseCurrency] = 1m;
        return rates;
    }

    public static decimal ToBase(decimal amount, string? currency, IReadOnlyDictionary<string, decimal> rates)
    {
        var normalized = Normalize(currency);
        return rates.TryGetValue(normalized, out var rate)
            ? amount * rate
            : amount;
    }

    public static string[] MissingCurrencies(IEnumerable<string?> currencies, IReadOnlyDictionary<string, decimal> rates) =>
        currencies
            .Select(Normalize)
            .Where(c => c.Length > 0 && c != BaseCurrency && !rates.ContainsKey(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToArray();

    public static string Normalize(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? BaseCurrency : currency.Trim().ToUpperInvariant();
}
