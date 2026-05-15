using LuminaVault.Data;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Validation;

/// Shared validation primitives. Each method returns null on success, or an IResult
/// with a BadRequest envelope on failure. Use FirstFailure() to compose multiple checks
/// without nesting.
public static class Validate
{
    public static IResult? Required(string? value, string fieldDisplayName) =>
        string.IsNullOrWhiteSpace(value)
            ? Problem.BadRequest($"{fieldDisplayName} is required.")
            : null;

    public static IResult? Positive(decimal value, string fieldDisplayName) =>
        value <= 0
            ? Problem.BadRequest($"{fieldDisplayName} must be greater than zero.")
            : null;

    public static IResult? NonNegative(decimal value, string fieldDisplayName) =>
        value < 0
            ? Problem.BadRequest($"{fieldDisplayName} cannot be negative.")
            : null;

    public static async Task<IResult?> FinanceAccountExists(
        AppDbContext db,
        int accountId,
        string error = "Choose a valid account.")
    {
        if (accountId <= 0) return Problem.BadRequest(error);
        var exists = await db.FinanceAccounts.AnyAsync(a => a.Id == accountId);
        return exists ? null : Problem.BadRequest(error);
    }

    /// Returns the first non-null IResult (i.e. the first validation failure),
    /// or null if every check passed. Lets endpoints stack checks linearly:
    ///   var failure = Validate.FirstFailure(
    ///       Validate.Required(input.Name, "Name"),
    ///       Validate.Positive(input.Amount, "Amount"));
    public static IResult? FirstFailure(params IResult?[] results)
    {
        foreach (var r in results) if (r is not null) return r;
        return null;
    }
}
