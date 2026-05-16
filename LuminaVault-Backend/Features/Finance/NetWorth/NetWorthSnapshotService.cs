using LuminaVault.Data;
using LuminaVault.Domain;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

/// Computes the current net worth from live state and upserts a snapshot row for today.
/// Idempotent on (SnapshotDate) — calling twice in one day overwrites the row in place,
/// so background ticks and a manual "snapshot now" share the same upsert path.
public class NetWorthSnapshotService
{
    public async Task<NetWorthSnapshot> CaptureAsync(AppDbContext db, CancellationToken ct = default)
    {
        var rates = await CurrencyConversion.LoadRates(db);

        var activeAccounts = await db.FinanceAccounts.Where(a => !a.IsArchived).ToListAsync(ct);
        var accountNetWorth = activeAccounts.Sum(a =>
            CurrencyConversion.ToBase(a.Balance, a.Currency, rates));

        var holdings = await db.Holdings.Include(h => h.Account).ToListAsync(ct);
        var holdingsMarketValue = holdings.Sum(h => CurrencyConversion.ToBase(
            h.LastPrice.HasValue ? h.Quantity * h.LastPrice.Value : h.Quantity * h.AverageCost,
            h.Account?.Currency,
            rates));
        var holdingsCostBasis = holdings.Sum(h =>
            CurrencyConversion.ToBase(h.Quantity * h.AverageCost, h.Account?.Currency, rates));

        var inventoryValue = await db.Items
            .Select(i => new { i.Value, i.Quantity })
            .ToListAsync(ct);
        var assetValue = inventoryValue.Sum(i => (i.Value ?? 0m) * i.Quantity);

        var today = DateTime.UtcNow.Date;
        var snapshot = await db.NetWorthSnapshots
            .FirstOrDefaultAsync(s => s.SnapshotDate == today, ct);
        if (snapshot is null)
        {
            snapshot = new NetWorthSnapshot { SnapshotDate = today };
            db.NetWorthSnapshots.Add(snapshot);
        }

        snapshot.AccountNetWorth = Math.Round(accountNetWorth, 2);
        snapshot.HoldingsMarketValue = Math.Round(holdingsMarketValue, 2);
        snapshot.HoldingsCostBasis = Math.Round(holdingsCostBasis, 2);
        snapshot.InventoryValue = Math.Round(assetValue, 2);
        snapshot.NetWorth = snapshot.AccountNetWorth + snapshot.HoldingsMarketValue + snapshot.InventoryValue;
        snapshot.Currency = CurrencyConversion.BaseCurrency;

        await db.SaveChangesAsync(ct);
        return snapshot;
    }

    public async Task<int> PruneAsync(AppDbContext db, int retentionDays, CancellationToken ct = default)
    {
        if (retentionDays <= 0) return 0;
        var cutoff = DateTime.UtcNow.Date.AddDays(-retentionDays);
        var stale = await db.NetWorthSnapshots
            .Where(s => s.SnapshotDate < cutoff)
            .ToListAsync(ct);
        if (stale.Count == 0) return 0;
        db.NetWorthSnapshots.RemoveRange(stale);
        await db.SaveChangesAsync(ct);
        return stale.Count;
    }
}
