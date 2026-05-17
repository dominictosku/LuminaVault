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
            FinanceTransactionKind? kind, DateTime? from, DateTime? to,
            string? cursor, int? pageSize) =>
        {
            // Read-only list: opt out of change tracking so we don't pay the per-row tax
            // on what's frequently the largest result set in the app.
            var query = db.FinanceTransactions
                .AsNoTracking()
                .Include(t => t.Account)
                .Include(t => t.TransferAccount)
                .Include(t => t.Splits)
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

            // Keyset pagination on (OccurredOn DESC, Id DESC). The cursor encodes the last
            // row of the prior page; everything strictly older comes next. Avoids OFFSET's
            // O(N) skip cost and stays stable if rows are inserted while paging.
            if (TransactionCursor.TryDecode(cursor, out var cursorDate, out var cursorId))
            {
                query = query.Where(t =>
                    t.OccurredOn < cursorDate ||
                    (t.OccurredOn == cursorDate && t.Id < cursorId));
            }

            var size = Math.Clamp(pageSize ?? 250, 1, 1000);
            var page = await query
                .OrderByDescending(t => t.OccurredOn)
                .ThenByDescending(t => t.Id)
                // +1 to detect whether more rows exist beyond this page without a separate count.
                .Take(size + 1)
                .ToListAsync();

            string? nextCursor = null;
            if (page.Count > size)
            {
                page.RemoveAt(page.Count - 1);
                var last = page[^1];
                nextCursor = TransactionCursor.Encode(last.OccurredOn, last.Id);
            }
            return Results.Ok(new
            {
                items = page.Select(MapTransaction),
                nextCursor,
            });
        });

        transactions.MapPost("/", async ([FromBody] FinanceTransactionInput input, AppDbContext db) =>
        {
            var validation = await ValidateTransaction(input, db);
            if (validation is not null) return validation;
            input = input with
            {
                Category = await FinanceCategoryRules.ResolveCategory(
                    db, input.Category, input.Payee, input.Description, input.Notes) ?? input.Category
            };

            var transaction = new FinanceTransaction();
            ApplyTransaction(transaction, input);
            db.FinanceTransactions.Add(transaction);
            await SaveAndRecalculateForTransaction(db, transaction.AccountId);

            await LoadTransactionRefs(db, transaction);
            return Results.Created($"/api/finance/transactions/{transaction.Id}", MapTransaction(transaction));
        });

        transactions.MapPut("/{id:int}", async (int id, [FromBody] FinanceTransactionInput input, AppDbContext db) =>
        {
            var transaction = await db.FinanceTransactions
                .Include(t => t.Account)
                .Include(t => t.TransferAccount)
                .Include(t => t.Splits)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (transaction is null) return Results.NotFound();
            var validation = await ValidateTransaction(input, db);
            if (validation is not null) return validation;
            input = input with
            {
                Category = await FinanceCategoryRules.ResolveCategory(
                    db, input.Category, input.Payee, input.Description, input.Notes) ?? input.Category
            };

            var previousAccountId = transaction.AccountId;
            ApplyTransaction(transaction, input);
            transaction.UpdatedAt = DateTime.UtcNow;
            await SaveAndRecalculateForTransaction(db, transaction.AccountId, previousAccountId);

            await LoadTransactionRefs(db, transaction);
            return Results.Ok(MapTransaction(transaction));
        });

        transactions.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var transaction = await db.FinanceTransactions.FindAsync(id);
            if (transaction is null) return Results.NotFound();
            var accountId = transaction.AccountId;
            db.FinanceTransactions.Remove(transaction);
            await SaveAndRecalculateForTransaction(db, accountId);

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

        // Replace splits wholesale. Validation has already ensured sum == Amount and the
        // kind is splittable, so we just rebuild the list from the input.
        transaction.Splits.Clear();
        if (SupportsSplits(input.Kind) && input.Splits is { Length: > 0 })
        {
            var ordered = input.Splits
                .Select((s, i) => new TransactionSplit
                {
                    Category = string.IsNullOrWhiteSpace(s.Category) ? "General" : s.Category.Trim(),
                    Amount = Math.Abs(s.Amount),
                    Notes = Clean(s.Notes),
                    SortOrder = i,
                });
            foreach (var split in ordered)
                transaction.Splits.Add(split);
        }
    }

    public static bool SupportsSplits(FinanceTransactionKind kind) =>
        kind is FinanceTransactionKind.Income or FinanceTransactionKind.Expense;

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

        if (input.Splits is { Length: > 0 })
        {
            if (!SupportsSplits(input.Kind))
                return Problem.BadRequest("Splits are only supported on Income and Expense transactions.");
            foreach (var split in input.Splits)
            {
                if (string.IsNullOrWhiteSpace(split.Category))
                    return Problem.BadRequest("Every split needs a category.");
                if (split.Amount <= 0)
                    return Problem.BadRequest("Split amounts must be greater than zero.");
            }
            // Tolerate 1-rappen rounding noise; the UI auto-fills the last split to balance.
            var splitTotal = input.Splits.Sum(s => Math.Abs(s.Amount));
            if (Math.Abs(splitTotal - Math.Abs(input.Amount)) > 0.01m)
                return Problem.BadRequest($"Splits must sum to {Math.Abs(input.Amount):F2}; got {splitTotal:F2}.");
        }
        return null;
    }
}

/// Opaque base64 cursor that pins keyset pagination to (OccurredOn, Id). Format is
/// kept simple + debuggable on purpose — base64-decoded, it reads as "yyyy-MM-dd|123".
/// Bad cursors decode to no-op rather than 400, so a client that hands us a stale or
/// fabricated cursor just gets the first page instead of a hard error.
internal static class TransactionCursor
{
    public static string Encode(DateTime occurredOn, int id) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            $"{occurredOn:yyyy-MM-dd}|{id}"));

    public static bool TryDecode(string? cursor, out DateTime occurredOn, out int id)
    {
        occurredOn = default;
        id = 0;
        if (string.IsNullOrWhiteSpace(cursor)) return false;
        try
        {
            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = decoded.Split('|', 2);
            if (parts.Length != 2) return false;
            if (!DateTime.TryParseExact(parts[0], "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out occurredOn)) return false;
            return int.TryParse(parts[1], out id);
        }
        catch
        {
            return false;
        }
    }
}
