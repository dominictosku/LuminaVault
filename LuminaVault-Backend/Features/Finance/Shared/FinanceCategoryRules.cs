using LuminaVault.Data;
using LuminaVault.Domain;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

internal static class FinanceCategoryRules
{
    private static readonly string[] PlaceholderCategories = ["", "General", "Imported", "Uncategorized"];

    public static bool ShouldApply(string? category) =>
        string.IsNullOrWhiteSpace(category) ||
        PlaceholderCategories.Any(c => string.Equals(c, category.Trim(), StringComparison.OrdinalIgnoreCase));

    public static async Task<string?> ResolveCategory(
        AppDbContext db,
        string? category,
        string? payee,
        string? description,
        string? notes)
    {
        if (!ShouldApply(category)) return Clean(category);
        var rules = await db.FinanceCategoryRules
            .Where(r => r.IsActive)
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.Pattern)
            .ToListAsync();
        return ResolveCategory(rules, category, payee, description, notes);
    }

    public static string? ResolveCategory(
        IEnumerable<FinanceCategoryRule> rules,
        string? category,
        string? payee,
        string? description,
        string? notes)
    {
        if (!ShouldApply(category)) return Clean(category);
        foreach (var rule in rules)
        {
            if (Matches(rule, payee, description, notes))
                return rule.Category;
        }
        return Clean(category);
    }

    private static bool Matches(FinanceCategoryRule rule, string? payee, string? description, string? notes)
    {
        var pattern = rule.Pattern.Trim();
        if (pattern.Length == 0) return false;
        return (rule.MatchPayee && Contains(payee, pattern)) ||
               (rule.MatchDescription && (Contains(description, pattern) || Contains(notes, pattern)));
    }

    private static bool Contains(string? value, string pattern) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Contains(pattern, StringComparison.OrdinalIgnoreCase);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
