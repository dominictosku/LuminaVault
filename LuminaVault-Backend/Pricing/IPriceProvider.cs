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
    Task<IReadOnlyList<PriceQuote>> GetQuotesAsync(
        IReadOnlyCollection<PriceLookup> lookups,
        CancellationToken ct = default);
}
