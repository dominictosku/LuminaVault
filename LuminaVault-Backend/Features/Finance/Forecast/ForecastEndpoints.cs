using LuminaVault.Data;

namespace LuminaVault.Endpoints;

internal static class ForecastEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/finance/forecast").RequireAuthorization().WithTags("Finance");

        group.MapGet("/", async (
            CashFlowForecastService service,
            AppDbContext db,
            int days = 30,
            int? accountId = null,
            CancellationToken ct = default) =>
        {
            var forecast = await service.BuildAsync(db, days, accountId, ct);
            return Results.Ok(forecast);
        });
    }
}
