using LuminaVault.Data;
using LuminaVault.Domain;
using LuminaVault.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static LuminaVault.Endpoints.FinanceHelpers;
using static LuminaVault.Endpoints.FinanceMappers;

namespace LuminaVault.Endpoints;

internal static class BalanceSnapshotEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var balanceSnapshots = app.MapGroup("/api/finance/balance-snapshots").RequireAuthorization().WithTags("Finance");

        balanceSnapshots.MapGet("/", async (AppDbContext db, int? accountId) =>
        {
            var query = db.AccountBalanceSnapshots.Include(s => s.Account).AsQueryable();
            if (accountId.HasValue) query = query.Where(s => s.AccountId == accountId.Value);
            var result = await query
                .OrderByDescending(s => s.SnapshotDate)
                .ThenBy(s => s.Account!.Name)
                .Take(200)
                .ToListAsync();
            return Results.Ok(result.Select(MapBalanceSnapshot));
        });

        balanceSnapshots.MapPost("/", async ([FromBody] AccountBalanceSnapshotInput input, AppDbContext db) =>
        {
            var validation = await ValidateBalanceSnapshot(input, db);
            if (validation is not null) return validation;
            var date = input.SnapshotDate.Date;
            if (await db.AccountBalanceSnapshots.AnyAsync(s => s.AccountId == input.AccountId && s.SnapshotDate == date))
                return Problem.Conflict("This account already has a snapshot for that date.");
            var expected = await ExpectedBalanceAt(db, input.AccountId, date);
            var snapshot = new AccountBalanceSnapshot();
            ApplyBalanceSnapshot(snapshot, input, expected);
            db.AccountBalanceSnapshots.Add(snapshot);
            await db.SaveChangesAsync();
            await RecalculateBalances(db);
            await db.Entry(snapshot).Reference(s => s.Account).LoadAsync();
            return Results.Created($"/api/finance/balance-snapshots/{snapshot.Id}", MapBalanceSnapshot(snapshot));
        });

        balanceSnapshots.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var snapshot = await db.AccountBalanceSnapshots.FindAsync(id);
            if (snapshot is null) return Results.NotFound();
            db.AccountBalanceSnapshots.Remove(snapshot);
            await db.SaveChangesAsync();
            await RecalculateBalances(db);
            return Results.NoContent();
        });
    }

    static void ApplyBalanceSnapshot(
        AccountBalanceSnapshot snapshot,
        AccountBalanceSnapshotInput input,
        decimal expected)
    {
        snapshot.AccountId = input.AccountId;
        snapshot.SnapshotDate = input.SnapshotDate.Date;
        snapshot.ActualBalance = input.ActualBalance;
        snapshot.ExpectedBalance = expected;
        snapshot.Difference = input.ActualBalance - expected;
        snapshot.IsReconciled = input.IsReconciled;
        snapshot.Notes = Clean(input.Notes);
    }

    static async Task<IResult?> ValidateBalanceSnapshot(AccountBalanceSnapshotInput input, AppDbContext db) =>
        await Validate.FinanceAccountExists(db, input.AccountId);
}
