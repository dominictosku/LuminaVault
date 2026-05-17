using static LuminaVault.Endpoints.OdsParsing;

namespace LuminaVault.Endpoints;

/// Canonical sheet names + alias resolution. The export side uses canonical names
/// like "Monthly summaries"; the import side accepts both English and German aliases
/// (and the mapped-import flow looks up a target by its normalised key).
internal static class OdsTargets
{
    /// Aliases tried in order for each entity, including legacy German names from
    /// before the schema was English-only. First match wins.
    public static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Accounts"] = new[] { "Accounts", "Konten" },
        ["Transactions"] = new[] { "Transactions" },
        ["Holdings"] = new[] { "Holdings", "Positionen" },
        ["Monthly summaries"] = new[] { "Monthly summaries", "MonthlySummaries", "Monatssummen" },
        ["Subscriptions"] = new[] { "Subscriptions", "Abos" },
        ["Finance categories"] = new[] { "Finance categories", "FinanceCategories", "Finanzkategorien" },
        ["Asset categories"] = new[] { "Asset categories", "AssetCategories", "Kategorien" },
        ["Assets"] = new[] { "Assets", "Inventar" },
        ["Loans"] = new[] { "Loans", "Kredite", "Darlehen" },
    };

    /// Maps a normalised import target name (from the preview/mapped-import flow)
    /// back to the canonical sheet display name.
    public static string CanonicalSheetName(string target) => Normalize(target) switch
    {
        "accounts" => "Accounts",
        "transactions" => "Transactions",
        "holdings" => "Holdings",
        "monthlysummaries" => "Monthly summaries",
        "subscriptions" => "Subscriptions",
        "financecategories" => "Finance categories",
        "assetcategories" => "Asset categories",
        "assets" => "Assets",
        "loans" => "Loans",
        _ => target
    };

    /// Best-guess target for the preview UI: looks at sheet name first, then column
    /// headers ("payee" → Transactions even on a weirdly-named sheet).
    public static string SuggestedTarget(string sheetName, string[] headers)
    {
        var name = Normalize(sheetName);
        var normalizedHeaders = headers.Select(Normalize).ToHashSet();
        if (name.Contains("transaction") || normalizedHeaders.Contains("payee")) return "Transactions";
        if (name.Contains("holding") || name.Contains("position") || normalizedHeaders.Contains("symbol")) return "Holdings";
        if (name.Contains("monthly") || name.Contains("monatssummen")) return "Monthly summaries";
        if (name.Contains("subscription") || name.Contains("abos")) return "Subscriptions";
        if (name.Contains("account") || name.Contains("konten")) return "Accounts";
        if (name.Contains("financecategor")) return "Finance categories";
        if (name.Contains("assetcategor") || name.Contains("kategorien")) return "Asset categories";
        if (name.Contains("asset") || name.Contains("inventar")) return "Assets";
        if (name.Contains("loan") || name.Contains("kredit") || name.Contains("darlehen")) return "Loans";
        return "";
    }

    /// Looks up a table by trying all aliases for `canonical`. Returns false if none match.
    public static bool TryGetByAlias(
        Dictionary<string, List<List<string>>> tables,
        string canonical,
        out List<List<string>> rows)
    {
        if (Aliases.TryGetValue(canonical, out var names))
        {
            foreach (var alias in names)
                if (tables.TryGetValue(alias, out rows!))
                    return true;
        }
        rows = null!;
        return false;
    }
}
