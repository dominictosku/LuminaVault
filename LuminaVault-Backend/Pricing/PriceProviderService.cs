using LuminaVault.Data;
using LuminaVault.Domain;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Pricing;

public class PriceProviderService
{
    private readonly IReadOnlyList<IPriceProvider> _providers;
    private readonly ILogger<PriceProviderService> _log;

    public PriceProviderService(IEnumerable<IPriceProvider> providers, ILogger<PriceProviderService> log)
    {
        _providers = providers.ToList();
        _log = log;
    }

    public IReadOnlyList<string> RegisteredProviderNames => _providers.Select(p => p.Name).ToList();

    public IPriceProvider? FindProvider(FinanceAccountType type) =>
        _providers.FirstOrDefault(p => p.Supports(type));

    public async Task<RefreshResult> RefreshAsync(AppDbContext db, int? accountId = null, CancellationToken ct = default)
    {
        var query = db.Holdings.Include(h => h.Account).AsQueryable();
        if (accountId.HasValue) query = query.Where(h => h.AccountId == accountId.Value);
        var holdings = (await query.ToListAsync(ct))
            .Where(h => h.Account != null && h.Quantity > 0)
            .ToList();

        var lookupsByProvider = new Dictionary<IPriceProvider, List<PriceLookup>>();
        var skipped = new List<RefreshSkipped>();

        foreach (var h in holdings)
        {
            var type = h.Account!.Type;
            var provider = FindProvider(type);
            if (provider is null)
            {
                skipped.Add(new RefreshSkipped(h.Id, h.Symbol, $"No price provider registered for {type} accounts"));
                continue;
            }
            if (!lookupsByProvider.TryGetValue(provider, out var bucket))
                lookupsByProvider[provider] = bucket = new List<PriceLookup>();
            bucket.Add(new PriceLookup(h.Id, h.Symbol, h.ProviderId, h.Account.Currency, type));
        }

        var quotes = new List<PriceQuote>();
        foreach (var (provider, batch) in lookupsByProvider)
        {
            try
            {
                quotes.AddRange(await provider.GetQuotesAsync(batch, ct));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Provider {Name} threw while fetching quotes", provider.Name);
                quotes.AddRange(batch.Select(l =>
                    new PriceQuote(l.HoldingId, null, $"{provider.Name} failed: {ex.Message}", null)));
            }
        }

        var byId = quotes.ToDictionary(q => q.HoldingId);
        var errors = new List<RefreshError>();
        var updated = 0;
        var now = DateTime.UtcNow;

        foreach (var h in holdings)
        {
            if (!byId.TryGetValue(h.Id, out var quote)) continue;
            if (quote.Price.HasValue && quote.Price.Value > 0)
            {
                h.LastPrice = quote.Price;
                h.LastPriceAt = now;
                if (string.IsNullOrWhiteSpace(h.ProviderId) && !string.IsNullOrWhiteSpace(quote.ResolvedProviderId))
                    h.ProviderId = quote.ResolvedProviderId;
                h.UpdatedAt = now;
                updated++;
            }
            else
            {
                errors.Add(new RefreshError(h.Id, h.Symbol, quote.Error ?? "No price returned"));
            }
        }

        if (updated > 0) await db.SaveChangesAsync(ct);
        return new RefreshResult(updated, errors, skipped, lookupsByProvider.Keys.Select(p => p.Name).ToList());
    }
}
