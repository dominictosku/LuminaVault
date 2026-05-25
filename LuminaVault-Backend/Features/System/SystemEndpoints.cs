using System.Net;
using LuminaVault.Data;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystem(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", () => Results.Ok(new { app = "LuminaVault", version = "1.0" }))
            .RequireAuthorization();

        // Authenticated API healthcheck for operators.
        app.MapGet("/api/health", async (AppDbContext db, CancellationToken ct) =>
            await CheckHealth(db, ct))
            .WithTags("Health")
            .RequireAuthorization();

        // Unauthenticated container-only healthcheck. The public nginx config does not proxy
        // /internal/*, and the endpoint refuses non-loopback callers even on the Docker network.
        app.MapGet("/internal/health", async (HttpContext ctx, AppDbContext db, CancellationToken ct) =>
        {
            var remoteIp = ctx.Connection.RemoteIpAddress;
            if (remoteIp is null || !IPAddress.IsLoopback(remoteIp))
                return Results.NotFound();

            return await CheckHealth(db, ct);
        })
        .WithTags("Health")
        .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> CheckHealth(AppDbContext db, CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
            return Results.Ok(new { status = "ok", database = "ok" });
        }
        catch (Exception ex)
        {
            return Results.Json(
                new { status = "degraded", database = "error", message = ex.Message },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
