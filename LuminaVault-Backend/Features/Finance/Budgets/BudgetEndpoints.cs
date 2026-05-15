using LuminaVault.Data;
using LuminaVault.Domain;
using LuminaVault.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static LuminaVault.Endpoints.FinanceHelpers;
using static LuminaVault.Endpoints.FinanceMappers;

namespace LuminaVault.Endpoints;

internal static class BudgetEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var budgets = app.MapGroup("/api/finance/budgets").RequireAuthorization().WithTags("Finance");

        budgets.MapGet("/", async (AppDbContext db, DateTime? month) =>
        {
            var targetMonth = MonthStart(month ?? DateTime.UtcNow);
            var spent = await CategorySpending(db, targetMonth);
            var result = await db.FinanceBudgets
                .Where(b => b.Month == targetMonth)
                .OrderBy(b => b.Category)
                .ToListAsync();
            return Results.Ok(result.Select(b => MapBudget(b, spent.GetValueOrDefault(b.Category, 0m))));
        });

        budgets.MapGet("/overview", async (AppDbContext db, DateTime? month) =>
        {
            var targetMonth = MonthStart(month ?? DateTime.UtcNow);
            var spent = await CategorySpending(db, targetMonth);
            var budgetsForMonth = await db.FinanceBudgets
                .Where(b => b.Month == targetMonth)
                .OrderBy(b => b.Category)
                .ToListAsync();
            var rows = budgetsForMonth.Select(b => MapBudget(b, spent.GetValueOrDefault(b.Category, 0m))).ToList();
            return Results.Ok(new
            {
                month = targetMonth,
                totalBudget = rows.Sum(r => r.LimitAmount),
                totalSpent = rows.Sum(r => r.Spent),
                remaining = rows.Sum(r => r.Remaining),
                rows
            });
        });

        budgets.MapPost("/", async ([FromBody] FinanceBudgetInput input, AppDbContext db) =>
        {
            var validation = ValidateBudget(input);
            if (validation is not null) return validation;
            var month = MonthStart(input.Month);
            if (await db.FinanceBudgets.AnyAsync(b => b.Category == input.Category.Trim() && b.Month == month))
                return Problem.Conflict("This category already has a budget for that month.");
            var budget = new FinanceBudget();
            ApplyBudget(budget, input);
            db.FinanceBudgets.Add(budget);
            await db.SaveChangesAsync();
            var spent = await CategorySpending(db, budget.Month);
            return Results.Created($"/api/finance/budgets/{budget.Id}", MapBudget(budget, spent.GetValueOrDefault(budget.Category, 0m)));
        });

        budgets.MapPut("/{id:int}", async (int id, [FromBody] FinanceBudgetInput input, AppDbContext db) =>
        {
            var budget = await db.FinanceBudgets.FindAsync(id);
            if (budget is null) return Results.NotFound();
            var validation = ValidateBudget(input);
            if (validation is not null) return validation;
            var category = input.Category.Trim();
            var month = MonthStart(input.Month);
            if (await db.FinanceBudgets.AnyAsync(b => b.Id != id && b.Category == category && b.Month == month))
                return Problem.Conflict("This category already has a budget for that month.");
            ApplyBudget(budget, input);
            budget.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            var spent = await CategorySpending(db, budget.Month);
            return Results.Ok(MapBudget(budget, spent.GetValueOrDefault(budget.Category, 0m)));
        });

        budgets.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var budget = await db.FinanceBudgets.FindAsync(id);
            if (budget is null) return Results.NotFound();
            db.FinanceBudgets.Remove(budget);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    static void ApplyBudget(FinanceBudget budget, FinanceBudgetInput input)
    {
        budget.Category = input.Category.Trim();
        budget.Month = MonthStart(input.Month);
        budget.LimitAmount = Math.Abs(input.LimitAmount);
        budget.Notes = Clean(input.Notes);
    }

    static IResult? ValidateBudget(FinanceBudgetInput input) =>
        Validate.FirstFailure(
            Validate.Required(input.Category, "Category"),
            Validate.Positive(input.LimitAmount, "Budget amount"));
}
