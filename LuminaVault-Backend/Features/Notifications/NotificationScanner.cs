using LuminaVault.Data;
using LuminaVault.Domain;
using Microsoft.EntityFrameworkCore;
using static LuminaVault.Endpoints.FinanceHelpers;

namespace LuminaVault.Endpoints;

/// Pure rule evaluators. Each one queries the DB, decides whether the condition
/// holds, and routes the result through NotificationService for dedup. Adding a
/// new alert means writing one more `Scan*` method and calling it from `ScanAll`.
public class NotificationScanner
{
    private readonly NotificationService _service;
    private readonly NotificationOptions _options;
    private readonly CashFlowForecastService _forecast;

    public NotificationScanner(
        NotificationService service,
        NotificationOptions options,
        CashFlowForecastService forecast)
    {
        _service = service;
        _options = options;
        _forecast = forecast;
    }

    public async Task<int> ScanAllAsync(AppDbContext db, CancellationToken ct = default)
    {
        var created = 0;
        created += await ScanSubscriptionsDueSoonAsync(db, ct);
        created += await ScanBudgetOverrunsAsync(db, ct);
        created += await ScanForecastNegativeAsync(db, ct);
        return created;
    }

    public async Task<int> ScanSubscriptionsDueSoonAsync(AppDbContext db, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var horizon = today.AddDays(Math.Max(1, _options.SubscriptionDueLookAheadDays));
        var subs = await db.Subscriptions
            .Include(s => s.Account)
            .Where(s => s.Status == SubscriptionStatus.Active && s.AutoRenew)
            .Where(s => s.NextDueOn >= today && s.NextDueOn <= horizon)
            .ToListAsync(ct);

        var created = 0;
        foreach (var sub in subs)
        {
            var source = $"subscription:{sub.Id}:due:{sub.NextDueOn:yyyy-MM-dd}";
            var days = (sub.NextDueOn.Date - today).Days;
            var when = days == 0 ? "today" : days == 1 ? "tomorrow" : $"in {days} days";
            var account = sub.Account?.Name ?? "no account";
            await _service.UpsertAsync(db, source,
                type: "SubscriptionDue",
                severity: NotificationSeverity.Info,
                title: $"{sub.Name} due {when}",
                message: $"{sub.Amount:F2} {sub.Currency} from {account} on {sub.NextDueOn:yyyy-MM-dd}.",
                link: "/subscriptions",
                ct: ct);
            created++;
        }
        return created;
    }

    public async Task<int> ScanBudgetOverrunsAsync(AppDbContext db, CancellationToken ct = default)
    {
        var month = MonthStart(DateTime.UtcNow);
        var budgets = await db.FinanceBudgets.Where(b => b.Month == month).ToListAsync(ct);
        if (budgets.Count == 0) return 0;

        // Reuse the same spend calc the budgets page shows: Expense-kind cleared/reconciled
        // transactions in the month, grouped by category. Pending tx are excluded.
        var nextMonth = month.AddMonths(1);
        var monthExpenses = await db.FinanceTransactions
            .Where(t => t.Kind == FinanceTransactionKind.Expense)
            .Where(t => t.Status != FinanceTransactionStatus.Pending)
            .Where(t => t.OccurredOn >= month && t.OccurredOn < nextMonth)
            .GroupBy(t => t.Category)
            .Select(g => new { Category = g.Key, Spent = g.Sum(t => t.Amount) })
            .ToListAsync(ct);
        var spentByCategory = monthExpenses.ToDictionary(x => x.Category ?? "", x => x.Spent);

        var created = 0;
        foreach (var b in budgets)
        {
            var spent = spentByCategory.GetValueOrDefault(b.Category, 0m);
            if (spent <= b.LimitAmount) continue;
            var over = spent - b.LimitAmount;
            var source = $"budget:{b.Category}:{b.Month:yyyy-MM}:overrun";
            await _service.UpsertAsync(db, source,
                type: "BudgetOverrun",
                severity: NotificationSeverity.Warning,
                title: $"Budget exceeded: {b.Category}",
                message: $"Spent {spent:F2} of {b.LimitAmount:F2} ({over:F2} over) this month.",
                link: "/budgets",
                ct: ct);
            created++;
        }
        return created;
    }

    public async Task<int> ScanForecastNegativeAsync(AppDbContext db, CancellationToken ct = default)
    {
        var forecast = await _forecast.BuildAsync(db, _options.ForecastLookAheadDays, accountId: null, ct);
        if (forecast.LowestBalance >= 0) return 0;

        // Dedup per low-date so re-running mid-day doesn't re-fire, but if the dip
        // moves to a different day on the next scan we surface a fresh alert.
        var source = $"forecast:negative:{forecast.LowestDate:yyyy-MM-dd}";
        await _service.UpsertAsync(db, source,
            type: "ForecastNegative",
            severity: NotificationSeverity.Critical,
            title: $"Cash projected to dip below zero on {forecast.LowestDate:MMM d}",
            message: $"Total liquid cash is forecast to reach {forecast.LowestBalance:F2} {forecast.BaseCurrency} " +
                     $"in the next {forecast.Days} days. Check the forecast page for the offending events.",
            link: "/forecast",
            ct: ct);
        return 1;
    }
}
