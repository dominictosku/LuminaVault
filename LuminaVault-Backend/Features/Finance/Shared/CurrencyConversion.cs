using LuminaVault.Data;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

internal static class CurrencyConversion
{
    public const string BaseCurrency = "CHF";

    public static async Task<Dictionary<string, decimal>> LoadRates(AppDbContext db)
    {
        var rateHistory = await LoadRateHistory(db);
        var rates = rateHistory.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.OrderByDescending(r => r.EffectiveDate).First().RateToBase,
            StringComparer.OrdinalIgnoreCase);
        rates[BaseCurrency] = 1m;
        return rates;
    }

    public static async Task<Dictionary<string, List<ExchangeRatePoint>>> LoadRateHistory(AppDbContext db)
    {
        var rows = await db.ExchangeRates
            .OrderBy(r => r.Currency)
            .ThenBy(r => r.EffectiveDate)
            .Select(r => new ExchangeRatePoint(Normalize(r.Currency), r.EffectiveDate, r.RateToBase))
            .ToListAsync();
        var result = rows
            .GroupBy(r => r.Currency, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        result[BaseCurrency] = [new ExchangeRatePoint(BaseCurrency, DateTime.MinValue, 1m)];
        return result;
    }

    public static decimal ToBase(decimal amount, string? currency, IReadOnlyDictionary<string, decimal> rates)
    {
        var normalized = Normalize(currency);
        return rates.TryGetValue(normalized, out var rate)
            ? amount * rate
            : amount;
    }

    public static decimal ToBase(
        decimal amount,
        string? currency,
        DateTime date,
        IReadOnlyDictionary<string, List<ExchangeRatePoint>> rateHistory)
    {
        var normalized = Normalize(currency);
        if (!rateHistory.TryGetValue(normalized, out var rates))
            return amount;
        var rate = rates
            .Where(r => r.EffectiveDate.Date <= date.Date)
            .OrderByDescending(r => r.EffectiveDate)
            .FirstOrDefault()
            ?? rates.OrderBy(r => r.EffectiveDate).FirstOrDefault();
        return rate is null ? amount : amount * rate.RateToBase;
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

internal record ExchangeRatePoint(string Currency, DateTime EffectiveDate, decimal RateToBase);
