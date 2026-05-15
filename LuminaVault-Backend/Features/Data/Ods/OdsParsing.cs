using System.Globalization;

namespace LuminaVault.Endpoints;

/// Primitives shared by the ODS export/import pipelines: cell value parsing,
/// header-row resolution, dictionary-style row lookup, and normalised key matching.
///
/// These are deliberately liberal — ODS sheets come from users and may have currency
/// prefixes, Swiss thousand separators, German date formats, alternative header names, etc.
internal static class OdsParsing
{
    public static (Dictionary<string, int> Headers, IEnumerable<List<string>> Data) SplitHeader(List<List<string>> rows)
    {
        var headerRow = rows.FirstOrDefault(r => r.Count(c => !string.IsNullOrWhiteSpace(c)) >= 2) ?? new List<string>();
        var headers = headerRow
            .Select((name, index) => new { name = Normalize(name), index })
            .Where(x => x.name.Length > 0)
            .GroupBy(x => x.name)
            .ToDictionary(g => g.Key, g => g.First().index);
        return (headers, rows.Skip(rows.IndexOf(headerRow) + 1));
    }

    /// Look up a cell by any of several header aliases. Header keys are normalised
    /// (lowercase + letters/digits only), so "Started on" matches "started_on", "STARTEDON", etc.
    public static string? Get(List<string> row, Dictionary<string, int> headers, params string[] names)
    {
        foreach (var name in names.Select(Normalize))
            if (headers.TryGetValue(name, out var index) && index < row.Count)
                return row[index].Trim();
        return null;
    }

    public static bool TryGetTable(
        Dictionary<string, List<List<string>>> tables,
        string name,
        out List<List<string>> rows) =>
        tables.TryGetValue(name, out rows!);

    public static string Normalize(string? value) =>
        new((value ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    public static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static DateTime MonthStart(DateTime value) => new(value.Year, value.Month, 1);

    public static string SummaryKey(int accountId, DateTime month) =>
        $"{accountId}:{MonthStart(month):yyyy-MM-dd}";

    public static bool HasSummaryFor(int accountId, DateTime date, HashSet<string> summaryKeys) =>
        summaryKeys.Contains(SummaryKey(accountId, date));

    public static T ParseEnum<T>(string? value, T fallback) where T : struct =>
        Enum.TryParse<T>(value, true, out var parsed) ? parsed : fallback;

    public static bool ParseBool(string? value) =>
        value?.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) == true ||
        value?.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase) == true ||
        value?.Trim().Equals("ja", StringComparison.OrdinalIgnoreCase) == true ||
        value?.Trim() == "1";

    public static decimal? ParseNullableDecimal(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : ParseDecimal(value);

    public static decimal ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        var cleaned = value
            .Replace("CHF", "", StringComparison.OrdinalIgnoreCase)
            .Replace("€", "")
            .Replace("$", "")
            .Replace("'", "")
            .Trim();
        if (decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out var invariant))
            return invariant;
        if (decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.GetCultureInfo("de-CH"), out var swiss))
            return swiss;
        return 0;
    }

    public static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var formats = new[] { "yyyy-MM-dd", "dd.MM.yyyy", "dd.MM.yy", "MM/dd/yyyy" };
        if (DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var exact))
            return exact.Date;
        return DateTime.TryParse(value, CultureInfo.GetCultureInfo("de-CH"), DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed.Date
            : null;
    }
}
