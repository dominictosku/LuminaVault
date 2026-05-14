using LuminaVault.Domain;

namespace LuminaVault.Pricing;

public record PriceLookup(
    int HoldingId,
    string Symbol,
    string? ProviderId,
    string Currency,
    FinanceAccountType AccountType);

public record PriceQuote(
    int HoldingId,
    decimal? Price,
    string? Error,
    string? ResolvedProviderId);

public record RefreshError(int HoldingId, string Symbol, string Error);
public record RefreshSkipped(int HoldingId, string Symbol, string Reason);
public record RefreshResult(
    int Updated,
    IReadOnlyList<RefreshError> Errors,
    IReadOnlyList<RefreshSkipped> Skipped,
    IReadOnlyList<string> Providers);

public interface IPriceProvider
{
    string Name { get; }
    bool Supports(FinanceAccountType accountType);

    /// True when the provider has everything it needs to fetch quotes (API key, etc.).
    /// Returning false makes the dispatcher skip it with a clear "not configured" message,
    /// so users don't have to trigger a refresh to discover the misconfiguration.
    bool IsConfigured { get; }

    Task<IReadOnlyList<PriceQuote>> GetQuotesAsync(
        IReadOnlyCollection<PriceLookup> lookups,
        CancellationToken ct = default);
}

public record ProviderStatus(string Name, bool IsConfigured, IReadOnlyList<string> SupportedAccountTypes);
