using LuminaVault.Data;
using LuminaVault.Domain;
using LuminaVault.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static LuminaVault.Endpoints.FinanceHelpers;
using static LuminaVault.Endpoints.FinanceMappers;

namespace LuminaVault.Endpoints;

internal static class GoalEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var goals = app.MapGroup("/api/finance/goals").RequireAuthorization().WithTags("Finance");

        goals.MapGet("/", async (AppDbContext db, bool includeInactive = false) =>
        {
            var query = db.SavingsGoals.Include(g => g.Account).AsQueryable();
            if (!includeInactive)
                query = query.Where(g => g.Status == SavingsGoalStatus.Active || g.Status == SavingsGoalStatus.Paused);
            var result = await query
                .OrderBy(g => g.Status == SavingsGoalStatus.Active ? 0 : 1)
                .ThenBy(g => g.TargetDate ?? DateTime.MaxValue)
                .ThenBy(g => g.Name)
                .ToListAsync();
            return Results.Ok(result.Select(MapGoal));
        });

        goals.MapPost("/", async ([FromBody] SavingsGoalInput input, AppDbContext db) =>
        {
            var validation = await ValidateGoal(input, db);
            if (validation is not null) return validation;

            var goal = new SavingsGoal();
            ApplyGoal(goal, input);
            db.SavingsGoals.Add(goal);
            await db.SaveChangesAsync();
            await db.Entry(goal).Reference(g => g.Account).LoadAsync();
            return Results.Created($"/api/finance/goals/{goal.Id}", MapGoal(goal));
        });

        goals.MapPut("/{id:int}", async (int id, [FromBody] SavingsGoalInput input, AppDbContext db) =>
        {
            var goal = await db.SavingsGoals.Include(g => g.Account).FirstOrDefaultAsync(g => g.Id == id);
            if (goal is null) return Results.NotFound();
            var validation = await ValidateGoal(input, db);
            if (validation is not null) return validation;

            ApplyGoal(goal, input);
            goal.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await db.Entry(goal).Reference(g => g.Account).LoadAsync();
            return Results.Ok(MapGoal(goal));
        });

        goals.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var goal = await db.SavingsGoals.FindAsync(id);
            if (goal is null) return Results.NotFound();
            db.SavingsGoals.Remove(goal);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static void ApplyGoal(SavingsGoal goal, SavingsGoalInput input)
    {
        goal.Name = input.Name.Trim();
        goal.AccountId = input.AccountId;
        goal.Currency = Clean(input.Currency)?.ToUpperInvariant() ?? "CHF";
        goal.TargetAmount = Math.Abs(input.TargetAmount);
        goal.CurrentAmount = Math.Max(0, input.CurrentAmount);
        goal.TargetDate = input.TargetDate?.Date;
        goal.Status = input.Status;
        goal.Notes = Clean(input.Notes);
    }

    private static async Task<IResult?> ValidateGoal(SavingsGoalInput input, AppDbContext db)
    {
        var basic = Validate.FirstFailure(
            Validate.Required(input.Name, "Goal name"),
            Validate.Positive(input.TargetAmount, "Target amount"));
        if (basic is not null) return basic;
        if (input.AccountId is { } accountId)
            return await Validate.FinanceAccountExists(db, accountId);
        return null;
    }
}
