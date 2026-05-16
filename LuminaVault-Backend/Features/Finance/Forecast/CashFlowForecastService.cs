using LuminaVault.Data;
using LuminaVault.Domain;
using Microsoft.EntityFrameworkCore;
using static LuminaVault.Endpoints.FinanceHelpers;

namespace LuminaVault.Endpoints;

/// Projects daily total cash forward by replaying pending transactions and rolling
/// subscriptions forward by their billing interval. Trend estimation from historical
/// spending is intentionally out of scope here — this forecast is "what's already
/// scheduled," not a Monte Carlo. That keeps it explicable: every dip on the chart
/// maps to a row in Events.
public class CashFlowForecastService
{
    public async Task<CashFlowForecast> BuildAsync(
        AppDbContext db,
        int days,
        int? accountId = null,
        CancellationToken ct = default)
    {
        days = Math.Clamp(days, 7, 365);
        var today = DateTime.UtcNow.Date;
        var to = today.AddDays(days);

        var rates = await CurrencyConversion.LoadRates(db);

        var accountsQuery = db.FinanceAccounts.Where(a => !a.IsArchived);
        if (accountId.HasValue) accountsQuery = accountsQuery.Where(a => a.Id == accountId.Value);
        var accounts = await accountsQuery.ToListAsync(ct);
        var accountsById = accounts.ToDictionary(a => a.Id);
        var startingBalance = accounts.Sum(a => CurrencyConversion.ToBase(a.Balance, a.Currency, rates));

        // Pending tx in window. Filter on relevant accounts only (covers transfer destination too).
        var pending = await db.FinanceTransactions
            .Include(t => t.Account)
            .Include(t => t.TransferAccount)
            .Where(t => t.Status == FinanceTransactionStatus.Pending)
            .Where(t => t.OccurredOn >= today && t.OccurredOn <= to)
            .Where(t => !accountId.HasValue
                || t.AccountId == accountId.Value
                || t.TransferAccountId == accountId.Value)
            .ToListAsync(ct);
        var pendingTagSet = pending
            .Where(t => !string.IsNullOrEmpty(t.TagsCsv))
            .SelectMany(t => t.TagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet();

        var subscriptions = await db.Subscriptions
            .Include(s => s.Account)
            .Where(s => s.Status == SubscriptionStatus.Active && s.AutoRenew && s.AccountId != null)
            .Where(s => !accountId.HasValue || s.AccountId == accountId.Value)
            .ToListAsync(ct);

        var events = new List<ForecastEvent>();

        foreach (var t in pending)
        {
            if (!accountsById.TryGetValue(t.AccountId, out var account)) continue;
            var primarySigned = SignedAmountForPrimaryAccount(t);
            var label = string.IsNullOrWhiteSpace(t.Payee) ? "(no payee)" : t.Payee;
            events.Add(new ForecastEvent(
                t.OccurredOn.Date,
                "Pending",
                $"{t.Kind} · {label}",
                t.AccountId,
                account.Name,
                primarySigned,
                account.Currency,
                CurrencyConversion.ToBase(primarySigned, account.Currency, rates)));

            if (t.Kind == FinanceTransactionKind.Transfer
                && t.TransferAccountId.HasValue
                && accountsById.TryGetValue(t.TransferAccountId.Value, out var dest))
            {
                events.Add(new ForecastEvent(
                    t.OccurredOn.Date,
                    "Transfer",
                    $"Transfer from {account.Name} · {label}",
                    t.TransferAccountId,
                    dest.Name,
                    t.Amount,
                    dest.Currency,
                    CurrencyConversion.ToBase(t.Amount, dest.Currency, rates)));
            }
        }

        foreach (var sub in subscriptions)
        {
            if (!accountsById.TryGetValue(sub.AccountId!.Value, out var account)) continue;
            var interval = Math.Max(1, sub.BillingIntervalDays);
            var dueDate = sub.NextDueOn.Date;
            // Catch up if NextDueOn drifted into the past (e.g. user paused syncing).
            while (dueDate < today) dueDate = dueDate.AddDays(interval);

            var cycles = 0;
            while (dueDate <= to && cycles < 366)
            {
                cycles++;
                var tag = SubscriptionTransactionTag(sub.Id, dueDate);
                if (!pendingTagSet.Contains(tag))
                {
                    events.Add(new ForecastEvent(
                        dueDate,
                        "Subscription",
                        sub.Provider is null ? sub.Name : $"{sub.Name} · {sub.Provider}",
                        sub.AccountId,
                        account.Name,
                        -sub.Amount,
                        sub.Currency,
                        -CurrencyConversion.ToBase(sub.Amount, sub.Currency, rates)));
                }
                dueDate = dueDate.AddDays(interval);
            }
        }

        events = events.OrderBy(e => e.Date).ThenBy(e => e.Source).ToList();

        // Daily roll-up. Initialize all days with running balance, fold in events as we go.
        var daily = new ForecastPoint[days + 1];
        var running = startingBalance;
        var previousDayBalance = startingBalance;
        var eventIdx = 0;
        var lowest = startingBalance;
        var lowestDate = today;

        for (var i = 0; i <= days; i++)
        {
            var date = today.AddDays(i);
            while (eventIdx < events.Count && events[eventIdx].Date <= date)
            {
                running += events[eventIdx].BaseAmount;
                eventIdx++;
            }
            var change = running - previousDayBalance;
            daily[i] = new ForecastPoint(date, Math.Round(running, 2), Math.Round(change, 2));
            previousDayBalance = running;
            if (running < lowest)
            {
                lowest = running;
                lowestDate = date;
            }
        }

        return new CashFlowForecast(
            GeneratedAt: DateTime.UtcNow,
            From: today,
            To: to,
            Days: days,
            BaseCurrency: CurrencyConversion.BaseCurrency,
            StartingBalance: Math.Round(startingBalance, 2),
            EndingBalance: Math.Round(running, 2),
            NetChange: Math.Round(running - startingBalance, 2),
            LowestBalance: Math.Round(lowest, 2),
            LowestDate: lowestDate,
            EventCount: events.Count,
            Daily: daily,
            Events: events.ToArray());
    }
}
