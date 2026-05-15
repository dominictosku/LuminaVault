using LuminaVault.Data;
using LuminaVault.Domain;
using LuminaVault.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static LuminaVault.Endpoints.FinanceHelpers;
using static LuminaVault.Endpoints.FinanceMappers;

namespace LuminaVault.Endpoints;

internal static class SubscriptionEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var subscriptions = app.MapGroup("/api/finance/subscriptions").RequireAuthorization().WithTags("Finance");

        subscriptions.MapGet("/", async (AppDbContext db, bool includeInactive = false) =>
        {
            var query = db.Subscriptions.Include(s => s.Account).Include(s => s.Attachments).AsQueryable();
            if (!includeInactive) query = query.Where(s => s.Status != SubscriptionStatus.Cancelled);
            var result = await query
                .OrderBy(s => s.Status == SubscriptionStatus.Active ? 0 : 1)
                .ThenBy(s => s.NextDueOn)
                .ToListAsync();
            return Results.Ok(result.Select(MapSubscription));
        });

        subscriptions.MapPost("/", async ([FromBody] SubscriptionInput input, AppDbContext db) =>
        {
            var validation = await ValidateSubscription(input, db);
            if (validation is not null) return validation;
            var subscription = new Subscription();
            ApplySubscription(subscription, input);
            db.Subscriptions.Add(subscription);
            await db.SaveChangesAsync();
            await db.Entry(subscription).Reference(s => s.Account).LoadAsync();
            return Results.Created($"/api/finance/subscriptions/{subscription.Id}", MapSubscription(subscription));
        });

        subscriptions.MapPut("/{id:int}", async (int id, [FromBody] SubscriptionInput input, AppDbContext db) =>
        {
            var subscription = await db.Subscriptions.Include(s => s.Account).Include(s => s.Attachments).FirstOrDefaultAsync(s => s.Id == id);
            if (subscription is null) return Results.NotFound();
            var validation = await ValidateSubscription(input, db);
            if (validation is not null) return validation;
            ApplySubscription(subscription, input);
            subscription.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await db.Entry(subscription).Reference(s => s.Account).LoadAsync();
            return Results.Ok(MapSubscription(subscription));
        });

        subscriptions.MapDelete("/{id:int}", async (int id, AppDbContext db, IWebHostEnvironment env) =>
        {
            var subscription = await db.Subscriptions.Include(s => s.Attachments).FirstOrDefaultAsync(s => s.Id == id);
            if (subscription is null) return Results.NotFound();
            foreach (var attachment in subscription.Attachments)
                AttachmentEndpoints.DeleteUploadFile(env, attachment.FileName);
            db.Subscriptions.Remove(subscription);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        subscriptions.MapPost("/{id:int}/generate-transaction", async (
            int id,
            [FromBody] SubscriptionGenerateTransactionInput input,
            AppDbContext db) =>
        {
            var subscription = await db.Subscriptions
                .Include(s => s.Account)
                .Include(s => s.Attachments)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (subscription is null) return Results.NotFound();
            if (subscription.AccountId is null)
                return Problem.BadRequest("Choose an account before generating transactions.");
            if (subscription.Status == SubscriptionStatus.Cancelled)
                return Problem.BadRequest("Cancelled subscriptions cannot generate transactions.");
            if (input.Status == FinanceTransactionStatus.Reconciled)
                return Problem.BadRequest("Generate as pending forecast or cleared transaction.");

            var tag = SubscriptionTransactionTag(subscription.Id, subscription.NextDueOn);
            var existing = await db.FinanceTransactions
                .Include(t => t.Account)
                .Include(t => t.TransferAccount)
                .FirstOrDefaultAsync(t => t.TagsCsv.Contains(tag));

            FinanceTransaction transaction;
            if (existing is not null)
            {
                if (input.Status == FinanceTransactionStatus.Cleared &&
                    existing.Status == FinanceTransactionStatus.Pending)
                {
                    existing.Status = FinanceTransactionStatus.Cleared;
                    existing.UpdatedAt = DateTime.UtcNow;
                    transaction = existing;
                }
                else
                {
                    return Problem.Conflict("A transaction for this subscription due date already exists.");
                }
            }
            else
            {
                transaction = new FinanceTransaction
                {
                    AccountId = subscription.AccountId.Value,
                    Kind = FinanceTransactionKind.Expense,
                    Status = input.Status,
                    OccurredOn = subscription.NextDueOn.Date,
                    Payee = subscription.Provider ?? subscription.Name,
                    Category = subscription.Category,
                    Amount = subscription.Amount,
                    Description = $"Generated from subscription: {subscription.Name}",
                    Notes = subscription.Notes,
                    TagsCsv = tag,
                };
                db.FinanceTransactions.Add(transaction);
            }

            if (input.AdvanceNextDueOn || input.Status == FinanceTransactionStatus.Cleared)
            {
                subscription.NextDueOn = AdvanceDueDate(subscription.NextDueOn, subscription.BillingIntervalDays);
                subscription.UpdatedAt = DateTime.UtcNow;
            }

            // generate-transaction writes a FinanceTransaction (new or status-updated),
            // bumps Subscription.NextDueOn, and then recomputes balances. Wrap in a
            // transaction so a crash can't leave a transaction inserted but the
            // subscription's NextDueOn not advanced (or vice versa).
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.SaveChangesAsync();
            await RecalculateBalances(db);
            await tx.CommitAsync();

            await LoadTransactionRefs(db, transaction);
            await db.Entry(subscription).Reference(s => s.Account).LoadAsync();
            return Results.Ok(new
            {
                transaction = MapTransaction(transaction),
                subscription = MapSubscription(subscription)
            });
        });
    }

    static void ApplySubscription(Subscription subscription, SubscriptionInput input)
    {
        subscription.Name = input.Name.Trim();
        subscription.Category = string.IsNullOrWhiteSpace(input.Category) ? "Subscriptions" : input.Category.Trim();
        subscription.Provider = Clean(input.Provider);
        subscription.AccountId = input.AccountId;
        subscription.Amount = Math.Abs(input.Amount);
        subscription.Currency = Clean(input.Currency)?.ToUpperInvariant() ?? "CHF";
        subscription.BillingIntervalDays = Math.Max(1, input.BillingIntervalDays);
        subscription.StartedOn = input.StartedOn.Date;
        subscription.NextDueOn = input.NextDueOn.Date;
        subscription.AutoRenew = input.AutoRenew;
        subscription.Status = input.Status;
        subscription.Notes = Clean(input.Notes);
    }

    static async Task<IResult?> ValidateSubscription(SubscriptionInput input, AppDbContext db)
    {
        var basic = Validate.FirstFailure(
            Validate.Required(input.Name, "Subscription name"),
            Validate.Positive(input.Amount, "Amount"));
        if (basic is not null) return basic;
        if (input.AccountId is { } accountId)
            return await Validate.FinanceAccountExists(db, accountId);
        return null;
    }
}
