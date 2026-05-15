using LuminaVault.Data;
using LuminaVault.Pricing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using static LuminaVault.Endpoints.FinanceHelpers;
using static LuminaVault.Endpoints.FinanceMappers;

namespace LuminaVault.Endpoints;

internal static class HoldingEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var holdings = app.MapGroup("/api/finance/holdings").RequireAuthorization().WithTags("Finance");

        holdings.MapGet("/", async (AppDbContext db, int? accountId) =>
        {
            var query = db.Holdings.Include(h => h.Account).AsQueryable();
            if (accountId.HasValue) query = query.Where(h => h.AccountId == accountId.Value);
            var result = await query
                .OrderBy(h => h.Account!.Name)
                .ThenBy(h => h.Symbol)
                .ToListAsync();
            var performance = await ComputePerformance(db, accountId);
            return Results.Ok(result.Select(h => MapHolding(
                h,
                performance.GetValueOrDefault(PerformanceKey(h.AccountId, h.Symbol)))));
        });

        holdings.MapPut("/{id:int}", async (int id, [FromBody] HoldingPriceInput input, AppDbContext db) =>
        {
            var holding = await db.Holdings.Include(h => h.Account).FirstOrDefaultAsync(h => h.Id == id);
            if (holding is null) return Results.NotFound();
            holding.LastPrice = input.LastPrice;
            holding.LastPriceAt = input.LastPrice.HasValue ? DateTime.UtcNow : null;
            holding.Name = Clean(input.Name);
            holding.ProviderId = Clean(input.ProviderId);
            holding.Notes = Clean(input.Notes);
            holding.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            var performance = await ComputePerformance(db, holding.AccountId);
            return Results.Ok(MapHolding(holding, performance.GetValueOrDefault(PerformanceKey(holding.AccountId, holding.Symbol))));
        });

        holdings.MapPost("/refresh-prices", async (
            PriceProviderService priceService,
            AppDbContext db,
            int? accountId,
            CancellationToken ct) =>
        {
            var result = await priceService.RefreshAsync(db, accountId, ct);
            return Results.Ok(result);
        })
        .RequireRateLimiting("price-refresh");

        // The /price-providers route lives outside the /holdings group but is conceptually
        // a holdings-feature concern (which providers are configured, which assets they cover).
        app.MapGet("/api/finance/price-providers", (PriceProviderService priceService) =>
            Results.Ok(priceService.GetStatuses()))
            .RequireAuthorization()
            .WithTags("Finance");

        holdings.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var holding = await db.Holdings.FindAsync(id);
            if (holding is null) return Results.NotFound();
            db.Holdings.Remove(holding);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        holdings.MapPost("/recompute", async (AppDbContext db) =>
        {
            var accountIds = await db.FinanceAccounts.Select(a => a.Id).ToListAsync();
            foreach (var accountId in accountIds)
                await RecalculateHoldings(db, accountId);
            return Results.NoContent();
        });
    }

    private static async Task<Dictionary<string, HoldingPerformance>> ComputePerformance(AppDbContext db, int? accountId)
    {
        var query = db.FinanceTransactions
            .Where(t => t.Status != LuminaVault.Domain.FinanceTransactionStatus.Pending)
            .Where(t => t.Symbol != null && t.Symbol != "")
            .Where(t => t.Kind == LuminaVault.Domain.FinanceTransactionKind.Buy ||
                        t.Kind == LuminaVault.Domain.FinanceTransactionKind.Sell ||
                        t.Kind == LuminaVault.Domain.FinanceTransactionKind.Dividend ||
                        t.Kind == LuminaVault.Domain.FinanceTransactionKind.Fee);
        if (accountId.HasValue) query = query.Where(t => t.AccountId == accountId.Value);

        var transactions = await query
            .OrderBy(t => t.OccurredOn)
            .ThenBy(t => t.Id)
            .ToListAsync();
        var result = new Dictionary<string, HoldingPerformance>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in transactions.GroupBy(t => PerformanceKey(t.AccountId, t.Symbol!)))
        {
            var accumulator = new HoldingPerformanceAccumulator();
            foreach (var t in group)
            {
                var quantity = t.Quantity ?? 0m;
                var price = t.PricePerUnit ?? (quantity > 0 ? t.Amount / quantity : 0m);
                switch (t.Kind)
                {
                    case LuminaVault.Domain.FinanceTransactionKind.Buy when quantity > 0:
                    {
                        var newQuantity = accumulator.Quantity + quantity;
                        accumulator.AverageCost = newQuantity > 0
                            ? (accumulator.Quantity * accumulator.AverageCost + quantity * price) / newQuantity
                            : 0m;
                        accumulator.Quantity = newQuantity;
                        break;
                    }
                    case LuminaVault.Domain.FinanceTransactionKind.Sell when quantity > 0:
                    {
                        var sold = accumulator.Quantity > 0 ? Math.Min(quantity, accumulator.Quantity) : quantity;
                        accumulator.RealizedPnL += sold * (price - accumulator.AverageCost);
                        accumulator.Quantity -= sold;
                        if (accumulator.Quantity <= 0)
                        {
                            accumulator.Quantity = 0;
                            accumulator.AverageCost = 0;
                        }
                        break;
                    }
                    case LuminaVault.Domain.FinanceTransactionKind.Dividend:
                        accumulator.Dividends += t.Amount;
                        break;
                    case LuminaVault.Domain.FinanceTransactionKind.Fee:
                        accumulator.Fees += t.Amount;
                        break;
                }
            }
            result[group.Key] = accumulator.ToPerformance();
        }

        return result;
    }

    private static string PerformanceKey(int accountId, string symbol) =>
        $"{accountId}:{symbol.ToUpperInvariant()}";

    private sealed class HoldingPerformanceAccumulator
    {
        public decimal Quantity { get; set; }
        public decimal AverageCost { get; set; }
        public decimal RealizedPnL { get; set; }
        public decimal Dividends { get; set; }
        public decimal Fees { get; set; }

        public HoldingPerformance ToPerformance() =>
            new(Math.Round(RealizedPnL, 2), Math.Round(Dividends, 2), Math.Round(Fees, 2));
    }
}
