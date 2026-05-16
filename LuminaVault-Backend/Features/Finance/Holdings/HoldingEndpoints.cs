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

        holdings.MapGet("/analytics", async (AppDbContext db, int? accountId) =>
        {
            var query = db.Holdings.Include(h => h.Account).AsQueryable();
            if (accountId.HasValue) query = query.Where(h => h.AccountId == accountId.Value);
            var holdingsResult = await query
                .OrderBy(h => h.Account!.Name)
                .ThenBy(h => h.Symbol)
                .ToListAsync();
            var rates = await CurrencyConversion.LoadRates(db);
            var performance = await ComputePerformance(db, accountId);

            var rows = holdingsResult.Select(h =>
            {
                var dto = MapHolding(h, performance.GetValueOrDefault(PerformanceKey(h.AccountId, h.Symbol)));
                var marketValue = dto.MarketValue ?? dto.CostBasis;
                var marketValueBase = CurrencyConversion.ToBase(marketValue, dto.Currency, rates);
                var costBasisBase = CurrencyConversion.ToBase(dto.CostBasis, dto.Currency, rates);
                var realizedBase = CurrencyConversion.ToBase(dto.RealizedPnL, dto.Currency, rates);
                var dividendsBase = CurrencyConversion.ToBase(dto.Dividends, dto.Currency, rates);
                var feesBase = CurrencyConversion.ToBase(dto.Fees, dto.Currency, rates);
                var totalReturnBase = CurrencyConversion.ToBase(dto.TotalReturn, dto.Currency, rates);
                return new
                {
                    dto.Id,
                    dto.Symbol,
                    dto.Name,
                    AccountName = dto.AccountName ?? "Unknown account",
                    MarketValue = Math.Round(marketValueBase, 2),
                    CostBasis = Math.Round(costBasisBase, 2),
                    UnrealizedPnL = Math.Round(marketValueBase - costBasisBase, 2),
                    RealizedPnL = Math.Round(realizedBase, 2),
                    Dividends = Math.Round(dividendsBase, 2),
                    Fees = Math.Round(feesBase, 2),
                    TotalReturn = Math.Round(totalReturnBase, 2),
                };
            }).ToList();

            var totalMarketValue = rows.Sum(r => r.MarketValue);
            var totalCostBasis = rows.Sum(r => r.CostBasis);
            var totalReturn = rows.Sum(r => r.TotalReturn);
            var allocationByAccount = rows
                .GroupBy(r => r.AccountName)
                .Select(g => Allocation(
                    g.Key,
                    g.Sum(r => r.MarketValue),
                    g.Sum(r => r.CostBasis),
                    g.Sum(r => r.TotalReturn),
                    totalMarketValue))
                .OrderByDescending(r => r.MarketValue)
                .ToArray();
            var allocationBySymbol = rows
                .GroupBy(r => r.Symbol)
                .Select(g => Allocation(
                    g.Key,
                    g.Sum(r => r.MarketValue),
                    g.Sum(r => r.CostBasis),
                    g.Sum(r => r.TotalReturn),
                    totalMarketValue))
                .OrderByDescending(r => r.MarketValue)
                .Take(10)
                .ToArray();
            var performers = rows
                .Select(r => new HoldingPerformerDto(
                    r.Id,
                    r.Symbol,
                    r.Name,
                    r.AccountName,
                    r.MarketValue,
                    r.CostBasis,
                    r.UnrealizedPnL,
                    r.TotalReturn,
                    r.CostBasis <= 0 ? null : Math.Round(r.TotalReturn / r.CostBasis * 100m, 2)))
                .ToList();

            return Results.Ok(new HoldingAnalyticsDto(
                DateTime.UtcNow,
                CurrencyConversion.BaseCurrency,
                new HoldingAnalyticsTotals(
                    Math.Round(totalMarketValue, 2),
                    Math.Round(totalCostBasis, 2),
                    Math.Round(rows.Sum(r => r.UnrealizedPnL), 2),
                    Math.Round(rows.Sum(r => r.RealizedPnL), 2),
                    Math.Round(rows.Sum(r => r.Dividends), 2),
                    Math.Round(rows.Sum(r => r.Fees), 2),
                    Math.Round(totalReturn, 2),
                    totalCostBasis <= 0 ? null : Math.Round(totalReturn / totalCostBasis * 100m, 2),
                    rows.Count),
                allocationByAccount,
                allocationBySymbol,
                performers.OrderByDescending(r => r.TotalReturn).Take(5).ToArray(),
                performers.OrderBy(r => r.TotalReturn).Take(5).ToArray()));
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

    private static HoldingAllocationDto Allocation(
        string name,
        decimal marketValue,
        decimal costBasis,
        decimal totalReturn,
        decimal totalMarketValue) =>
        new(
            name,
            Math.Round(marketValue, 2),
            Math.Round(costBasis, 2),
            Math.Round(totalReturn, 2),
            totalMarketValue <= 0 ? 0 : Math.Round(marketValue / totalMarketValue * 100m, 2));

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
