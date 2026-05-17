using LuminaVault.Data;
using LuminaVault.Domain;
using LuminaVault.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static LuminaVault.Endpoints.FinanceHelpers;
using static LuminaVault.Endpoints.FinanceMappers;

namespace LuminaVault.Endpoints;

internal static class MonthlySummaryEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var monthlySummaries = app.MapGroup("/api/finance/monthly-summaries").RequireAuthorization().WithTags("Finance");

        monthlySummaries.MapGet("/", async (AppDbContext db, int? accountId, DateTime? from, DateTime? to) =>
        {
            var query = db.MonthlyAccountSummaries.Include(s => s.Account).AsQueryable();
            if (accountId.HasValue) query = query.Where(s => s.AccountId == accountId.Value);
            if (from.HasValue) query = query.Where(s => s.Month >= MonthStart(from.Value));
            if (to.HasValue) query = query.Where(s => s.Month <= MonthStart(to.Value));

            var result = await query
                .OrderByDescending(s => s.Month)
                .ThenBy(s => s.Account!.Name)
                .Take(500)
                .ToListAsync();
            var expected = await ExpectedClosingBalances(db, result);
            return Results.Ok(result.Select(s => MapMonthlySummary(s, expected.GetValueOrDefault(s.Id))));
        });

        monthlySummaries.MapPost("/", async ([FromBody] MonthlyAccountSummaryInput input, AppDbContext db) =>
        {
            var validation = await ValidateMonthlySummary(input, db);
            if (validation is not null) return validation;

            var month = MonthStart(input.Month);
            if (await db.MonthlyAccountSummaries.AnyAsync(s => s.AccountId == input.AccountId && s.Month == month))
                return Problem.Conflict("This account already has a summary for that month.");

            var monthlySummary = new MonthlyAccountSummary();
            ApplyMonthlySummary(monthlySummary, input);
            db.MonthlyAccountSummaries.Add(monthlySummary);
            await SaveAndRecalculateBalances(db);

            await db.Entry(monthlySummary).Reference(s => s.Account).LoadAsync();
            var expected = await ExpectedBalanceAt(db, monthlySummary.AccountId, MonthEnd(monthlySummary.Month));
            return Results.Created($"/api/finance/monthly-summaries/{monthlySummary.Id}", MapMonthlySummary(monthlySummary, expected));
        });

        monthlySummaries.MapPut("/{id:int}", async (int id, [FromBody] MonthlyAccountSummaryInput input, AppDbContext db) =>
        {
            var monthlySummary = await db.MonthlyAccountSummaries
                .Include(s => s.Account)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (monthlySummary is null) return Results.NotFound();
            var validation = await ValidateMonthlySummary(input, db);
            if (validation is not null) return validation;

            var month = MonthStart(input.Month);
            if (await db.MonthlyAccountSummaries.AnyAsync(s =>
                s.Id != id && s.AccountId == input.AccountId && s.Month == month))
            {
                return Problem.Conflict("This account already has a summary for that month.");
            }

            ApplyMonthlySummary(monthlySummary, input);
            monthlySummary.UpdatedAt = DateTime.UtcNow;
            await SaveAndRecalculateBalances(db);

            await db.Entry(monthlySummary).Reference(s => s.Account).LoadAsync();
            var expected = await ExpectedBalanceAt(db, monthlySummary.AccountId, MonthEnd(monthlySummary.Month));
            return Results.Ok(MapMonthlySummary(monthlySummary, expected));
        });

        monthlySummaries.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var monthlySummary = await db.MonthlyAccountSummaries.FindAsync(id);
            if (monthlySummary is null) return Results.NotFound();
            db.MonthlyAccountSummaries.Remove(monthlySummary);
            await SaveAndRecalculateBalances(db);

            return Results.NoContent();
        });

        monthlySummaries.MapPost("/{id:int}/reconcile", async (int id, [FromBody] MonthlyReconciliationInput input, AppDbContext db) =>
        {
            var monthlySummary = await db.MonthlyAccountSummaries
                .Include(s => s.Account)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (monthlySummary is null) return Results.NotFound();
            if (input.IsReconciled && monthlySummary.ClosingBalance is null)
                return Problem.BadRequest("Add a closing balance before reconciling this month.");

            var endOfMonth = MonthEnd(monthlySummary.Month);
            var expected = await ExpectedBalanceAt(db, monthlySummary.AccountId, endOfMonth);
            monthlySummary.IsReconciled = input.IsReconciled;
            monthlySummary.ReconciledAt = input.IsReconciled ? DateTime.UtcNow : null;
            monthlySummary.ReconciliationNotes = Clean(input.Notes);
            monthlySummary.UpdatedAt = DateTime.UtcNow;

            var snapshot = await db.AccountBalanceSnapshots
                .FirstOrDefaultAsync(s => s.AccountId == monthlySummary.AccountId && s.SnapshotDate == endOfMonth);
            if (input.IsReconciled && monthlySummary.ClosingBalance.HasValue)
            {
                if (snapshot is null)
                {
                    snapshot = new AccountBalanceSnapshot { AccountId = monthlySummary.AccountId, SnapshotDate = endOfMonth };
                    db.AccountBalanceSnapshots.Add(snapshot);
                }
                snapshot.ActualBalance = monthlySummary.ClosingBalance.Value;
                snapshot.ExpectedBalance = expected;
                snapshot.Difference = monthlySummary.ClosingBalance.Value - expected;
                snapshot.IsReconciled = true;
                snapshot.Notes = Clean(input.Notes) ?? $"Reconciled {monthlySummary.Month:yyyy-MM}";
                snapshot.UpdatedAt = DateTime.UtcNow;
            }
            else if (!input.IsReconciled && snapshot is not null)
            {
                snapshot.IsReconciled = false;
                snapshot.Notes = Clean(input.Notes) ?? snapshot.Notes;
                snapshot.UpdatedAt = DateTime.UtcNow;
            }

            // Reconcile is a multi-step write: it updates the summary, creates/updates a
            // balance snapshot, and then recomputes account balances. Wrap so a crash
            // between any two of these can't leave the books inconsistent.
            await SaveAndRecalculateBalances(db);

            var refreshedExpected = await ExpectedBalanceAt(db, monthlySummary.AccountId, endOfMonth);
            return Results.Ok(MapMonthlySummary(monthlySummary, refreshedExpected));
        });
    }

    static void ApplyMonthlySummary(MonthlyAccountSummary monthlySummary, MonthlyAccountSummaryInput input)
    {
        monthlySummary.AccountId = input.AccountId;
        monthlySummary.Month = MonthStart(input.Month);
        monthlySummary.Income = Math.Abs(input.Income);
        monthlySummary.Expenses = Math.Abs(input.Expenses);
        monthlySummary.OpeningBalance = input.OpeningBalance;
        monthlySummary.ClosingBalance = input.ClosingBalance;
        monthlySummary.Notes = Clean(input.Notes);
    }

    static async Task<IResult?> ValidateMonthlySummary(MonthlyAccountSummaryInput input, AppDbContext db) =>
        Validate.FirstFailure(
            await Validate.FinanceAccountExists(db, input.AccountId),
            Validate.NonNegative(input.Income, "Income"),
            Validate.NonNegative(input.Expenses, "Expenses"));
}
