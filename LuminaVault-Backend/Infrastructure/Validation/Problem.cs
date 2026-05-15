namespace LuminaVault.Validation;

/// Consistent error response shape across all endpoints.
/// Keeps the wire format `{ "error": "message" }` that the Angular client reads
/// via `e?.error?.error` in 10+ pages.
public static class Problem
{
    public static IResult BadRequest(string error) => Results.BadRequest(new { error });
    public static IResult Conflict(string error) => Results.Conflict(new { error });
    public static IResult NotFound(string error) => Results.NotFound(new { error });
}
