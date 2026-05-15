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
            return Results.Ok(result.Select(MapHolding));
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
            return Results.Ok(MapHolding(holding));
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
}
