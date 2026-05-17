using LuminaVault.Data;
using LuminaVault.Domain;
using Microsoft.EntityFrameworkCore;
using static LuminaVault.Endpoints.FinanceHelpers;
using static LuminaVault.Endpoints.FinanceMappers;

namespace LuminaVault.Endpoints;

internal sealed class SubscriptionAutomationOptions
{
    public bool Enabled { get; set; }
    public int LookAheadDays { get; set; } = 7;
    public int IntervalHours { get; set; } = 24;
}

internal sealed class SubscriptionAutomationService(SubscriptionAutomationOptions options)
{
    public async Task<SubscriptionGenerateDueResult> GenerateDueAsync(
        AppDbContext db,
        int? lookAheadDays = null,
        CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var throughDate = today.AddDays(Math.Max(0, lookAheadDays ?? options.LookAheadDays));
        var subscriptions = await db.Subscriptions
            .Include(s => s.Account)
            .Include(s => s.Attachments)
            .Where(s => s.Status == SubscriptionStatus.Active)
            .Where(s => s.AutoRenew)
            .Where(s => s.NextDueOn <= throughDate)
            .OrderBy(s => s.NextDueOn)
            .ToListAsync(ct);

        var created = new List<FinanceTransaction>();
        var touchedSubscriptions = new HashSet<Subscription>();
        var skipped = 0;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var subscription in subscriptions)
        {
            if (subscription.AccountId is null)
            {
                skipped++;
                continue;
            }

            var cycles = 0;
            while (subscription.NextDueOn.Date <= throughDate && cycles < 60)
            {
                cycles++;
                var dueDate = subscription.NextDueOn.Date;
                var tag = SubscriptionTransactionTag(subscription.Id, dueDate);
                var exists = await db.FinanceTransactions.AnyAsync(t => t.TagsCsv.Contains(tag), ct);
                if (exists)
                {
                    skipped++;
                }
                else
                {
                    var transaction = new FinanceTransaction
                    {
                        AccountId = subscription.AccountId.Value,
                        Kind = FinanceTransactionKind.Expense,
                        Status = FinanceTransactionStatus.Pending,
                        OccurredOn = dueDate,
                        Payee = subscription.Provider ?? subscription.Name,
                        Category = subscription.Category,
                        Amount = subscription.Amount,
                        Description = $"Forecast from subscription: {subscription.Name}",
                        Notes = subscription.Notes,
                        TagsCsv = tag,
                    };
                    db.FinanceTransactions.Add(transaction);
                    created.Add(transaction);
                }

                subscription.NextDueOn = AdvanceDueDate(subscription);
                subscription.UpdatedAt = DateTime.UtcNow;
                touchedSubscriptions.Add(subscription);
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        foreach (var transaction in created)
            await LoadTransactionRefs(db, transaction);

        return new SubscriptionGenerateDueResult(
            DateTime.UtcNow,
            throughDate,
            created.Count,
            skipped,
            created.Select(MapTransaction).ToArray(),
            touchedSubscriptions.Select(MapSubscription).ToArray());
    }
}

internal sealed class SubscriptionAutomationBackgroundService(
    IServiceScopeFactory scopeFactory,
    SubscriptionAutomationOptions options,
    ILogger<SubscriptionAutomationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
            return;

        var interval = TimeSpan.FromHours(Math.Max(1, options.IntervalHours));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var automation = scope.ServiceProvider.GetRequiredService<SubscriptionAutomationService>();
                var result = await automation.GenerateDueAsync(db, options.LookAheadDays, stoppingToken);
                if (result.Created > 0)
                {
                    logger.LogInformation(
                        "Generated {Count} subscription forecast transactions through {ThroughDate:yyyy-MM-dd}",
                        result.Created,
                        result.ThroughDate);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled subscription automation failed");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
