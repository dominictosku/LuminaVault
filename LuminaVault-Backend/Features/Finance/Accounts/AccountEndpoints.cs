using LuminaVault.Data;
using LuminaVault.Domain;
using LuminaVault.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static LuminaVault.Endpoints.FinanceHelpers;
using static LuminaVault.Endpoints.FinanceMappers;

namespace LuminaVault.Endpoints;

internal static class AccountEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var accounts = app.MapGroup("/api/finance/accounts").RequireAuthorization().WithTags("Finance");

        accounts.MapGet("/", async (AppDbContext db, bool includeArchived = false) =>
        {
            var query = db.FinanceAccounts.AsQueryable();
            if (!includeArchived) query = query.Where(a => !a.IsArchived);
            var result = await query.OrderBy(a => a.IsArchived).ThenBy(a => a.Name).ToListAsync();
            return Results.Ok(result.Select(MapAccount));
        });

        accounts.MapPost("/", async ([FromBody] FinanceAccountInput input, AppDbContext db) =>
        {
            if (Validate.Required(input.Name, "Account name") is { } failure)
                return failure;

            var account = new FinanceAccount();
            ApplyAccount(account, input);
            account.Balance = input.Balance;
            account.StartingBalance = input.StartingBalance == 0 ? input.Balance : input.StartingBalance;
            db.FinanceAccounts.Add(account);
            await db.SaveChangesAsync();
            return Results.Created($"/api/finance/accounts/{account.Id}", MapAccount(account));
        });

        accounts.MapPut("/{id:int}", async (int id, [FromBody] FinanceAccountInput input, AppDbContext db) =>
        {
            var account = await db.FinanceAccounts.FindAsync(id);
            if (account is null) return Results.NotFound();
            ApplyAccount(account, input);
            await SaveAndRecalculateBalances(db);

            await db.Entry(account).ReloadAsync();
            return Results.Ok(MapAccount(account));
        });

        accounts.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var account = await db.FinanceAccounts.FindAsync(id);
            if (account is null) return Results.NotFound();
            var hasActivity = await db.FinanceTransactions.AnyAsync(t =>
                t.AccountId == id || t.TransferAccountId == id);
            if (hasActivity)
            {
                account.IsArchived = true;
            }
            else
            {
                db.FinanceAccounts.Remove(account);
            }
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    public static void ApplyAccount(FinanceAccount account, FinanceAccountInput input)
    {
        account.Name = input.Name.Trim();
        account.Institution = Clean(input.Institution);
        account.Type = input.Type;
        account.Currency = Clean(input.Currency)?.ToUpperInvariant() ?? "CHF";
        account.StartingBalance = input.StartingBalance;
        account.Color = string.IsNullOrWhiteSpace(input.Color) ? "#14b8a6" : input.Color.Trim();
        account.Notes = Clean(input.Notes);
        account.IsArchived = input.IsArchived;
    }
}
