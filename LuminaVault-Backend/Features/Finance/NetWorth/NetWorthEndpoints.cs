using LuminaVault.Data;
using LuminaVault.Domain;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

public record NetWorthSnapshotDto(
    int Id,
    DateTime SnapshotDate,
    decimal AccountNetWorth,
    decimal HoldingsMarketValue,
    decimal HoldingsCostBasis,
    decimal InventoryValue,
    decimal NetWorth,
    string Currency,
    DateTime CreatedAt);

internal static class NetWorthEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/finance/net-worth").RequireAuthorization().WithTags("Finance");

        group.MapGet("/history", async (AppDbContext db, DateTime? from, DateTime? to) =>
        {
            var query = db.NetWorthSnapshots.AsQueryable();
            if (from.HasValue) query = query.Where(s => s.SnapshotDate >= from.Value.Date);
            if (to.HasValue) query = query.Where(s => s.SnapshotDate <= to.Value.Date);
            var rows = await query
                .OrderBy(s => s.SnapshotDate)
                .Take(2000)
                .ToListAsync();
            return Results.Ok(rows.Select(Map));
        });

        group.MapPost("/snapshot", async (
            NetWorthSnapshotService service,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var snapshot = await service.CaptureAsync(db, ct);
            return Results.Ok(Map(snapshot));
        });
    }

    static NetWorthSnapshotDto Map(NetWorthSnapshot s) => new(
        s.Id, s.SnapshotDate, s.AccountNetWorth, s.HoldingsMarketValue,
        s.HoldingsCostBasis, s.InventoryValue, s.NetWorth, s.Currency, s.CreatedAt);
}
