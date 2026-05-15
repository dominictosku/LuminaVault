using LuminaVault.Data;
using LuminaVault.Domain;
using LuminaVault.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static LuminaVault.Endpoints.FinanceHelpers;
using static LuminaVault.Endpoints.FinanceMappers;

namespace LuminaVault.Endpoints;

internal static class TransactionEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var transactions = app.MapGroup("/api/finance/transactions").RequireAuthorization().WithTags("Finance");

        transactions.MapGet("/", async (
            AppDbContext db, string? q, int? accountId, string? category,
            FinanceTransactionKind? kind, DateTime? from, DateTime? to) =>
        {
            var query = db.FinanceTransactions
                .Include(t => t.Account)
                .Include(t => t.TransferAccount)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var needle = q.Trim().ToLower();
                query = query.Where(t =>
                    t.Payee.ToLower().Contains(needle) ||
                    t.Category.ToLower().Contains(needle) ||
                    (t.Description ?? "").ToLower().Contains(needle) ||
                    (t.Notes ?? "").ToLower().Contains(needle) ||
                    t.TagsCsv.ToLower().Contains(needle));
            }
            if (accountId.HasValue)
                query = query.Where(t => t.AccountId == accountId || t.TransferAccountId == accountId);
            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(t => t.Category == category);
            if (kind.HasValue)
                query = query.Where(t => t.Kind == kind);
            if (from.HasValue)
                query = query.Where(t => t.OccurredOn >= from.Value.Date);
            if (to.HasValue)
                query = query.Where(t => t.OccurredOn <= to.Value.Date);

            var result = await query
                .OrderByDescending(t => t.OccurredOn)
                .ThenByDescending(t => t.Id)
                .Take(800)
                .ToListAsync();
            return Results.Ok(result.Select(MapTransaction));
        });

        transactions.MapPost("/", async ([FromBody] FinanceTransactionInput input, AppDbContext db) =>
        {
            var validation = await ValidateTransaction(input, db);
            if (validation is not null) return validation;

            var transaction = new FinanceTransaction();
            ApplyTransaction(transaction, input);
            db.FinanceTransactions.Add(transaction);
            await db.SaveChangesAsync();
            await RecalculateHoldings(db, transaction.AccountId);
            await RecalculateBalances(db);
            await LoadTransactionRefs(db, transaction);
            return Results.Created($"/api/finance/transactions/{transaction.Id}", MapTransaction(transaction));
        });

        transactions.MapPut("/{id:int}", async (int id, [FromBody] FinanceTransactionInput input, AppDbContext db) =>
        {
            var transaction = await db.FinanceTransactions
                .Include(t => t.Account)
                .Include(t => t.TransferAccount)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (transaction is null) return Results.NotFound();
            var validation = await ValidateTransaction(input, db);
            if (validation is not null) return validation;

            var previousAccountId = transaction.AccountId;
            ApplyTransaction(transaction, input);
            transaction.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            if (previousAccountId != transaction.AccountId)
                await RecalculateHoldings(db, previousAccountId);
            await RecalculateHoldings(db, transaction.AccountId);
            await RecalculateBalances(db);
            await LoadTransactionRefs(db, transaction);
            return Results.Ok(MapTransaction(transaction));
        });

        transactions.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var transaction = await db.FinanceTransactions.FindAsync(id);
            if (transaction is null) return Results.NotFound();
            var accountId = transaction.AccountId;
            db.FinanceTransactions.Remove(transaction);
            await db.SaveChangesAsync();
            await RecalculateHoldings(db, accountId);
            await RecalculateBalances(db);
            return Results.NoContent();
        });
    }

    public static void ApplyTransaction(FinanceTransaction transaction, FinanceTransactionInput input)
    {
        transaction.AccountId = input.AccountId;
        transaction.TransferAccountId = input.Kind == FinanceTransactionKind.Transfer
            ? input.TransferAccountId
            : null;
        transaction.Kind = input.Kind;
        transaction.Status = input.Status;
        transaction.OccurredOn = input.OccurredOn.Date;
        transaction.Payee = input.Payee.Trim();
        transaction.Category = string.IsNullOrWhiteSpace(input.Category) ? "General" : input.Category.Trim();
        transaction.Amount = Math.Abs(input.Amount);
        transaction.Description = Clean(input.Description);
        transaction.Notes = Clean(input.Notes);
        transaction.TagsCsv = string.Join(",", (input.Tags ?? Array.Empty<string>())
            .Select(t => t.Trim()).Where(t => t.Length > 0));

        if (IsTradeKind(input.Kind))
        {
            transaction.Symbol = NormalizeSymbol(input.Symbol);
            transaction.Quantity = input.Quantity.HasValue ? Math.Abs(input.Quantity.Value) : null;
            transaction.PricePerUnit = input.PricePerUnit.HasValue ? Math.Abs(input.PricePerUnit.Value) : null;
        }
        else
        {
            transaction.Symbol = null;
            transaction.Quantity = null;
            transaction.PricePerUnit = null;
        }
    }

    public static async Task<IResult?> ValidateTransaction(FinanceTransactionInput input, AppDbContext db)
    {
        var basic = Validate.FirstFailure(
            await Validate.FinanceAccountExists(db, input.AccountId),
            Validate.Required(input.Payee, "Payee"),
            Validate.Positive(input.Amount, "Amount"));
        if (basic is not null) return basic;

        if (input.Kind == FinanceTransactionKind.Transfer)
        {
            if (input.TransferAccountId is null || input.TransferAccountId == input.AccountId)
                return Problem.BadRequest("Choose a different destination account.");
            if (!await db.FinanceAccounts.AnyAsync(a => a.Id == input.TransferAccountId.Value))
                return Problem.BadRequest("Choose a valid destination account.");
        }

        if (input.Kind == FinanceTransactionKind.Buy || input.Kind == FinanceTransactionKind.Sell)
        {
            return Validate.FirstFailure(
                Validate.Required(input.Symbol, "Symbol"),
                input.Quantity is not { } q || q <= 0
                    ? Problem.BadRequest("Quantity must be greater than zero.") : null,
                input.PricePerUnit is not { } p || p <= 0
                    ? Problem.BadRequest("Price per unit must be greater than zero.") : null);
        }
        return null;
    }
}
