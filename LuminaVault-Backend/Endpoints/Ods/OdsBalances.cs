using LuminaVault.Data;
using LuminaVault.Domain;
using Microsoft.EntityFrameworkCore;
using static LuminaVault.Endpoints.OdsParsing;

namespace LuminaVault.Endpoints;

/// Lightweight balance recompute used after a bulk ODS import.
///
/// Distinct from the production `FinanceHelpers.RecalculateBalances`: this one
/// ignores reconciled snapshots (imports happen on a snapshot-free state during
/// initial seeding) and ignores transaction Status (imports may bring in pending
/// rows that should still affect the imported balance).
internal static class OdsBalances
{
    public static async Task Recalculate(AppDbContext db)
    {
        var accounts = await db.FinanceAccounts.ToListAsync();
        var balances = accounts.ToDictionary(a => a.Id, a => a.StartingBalance);
        var summaries = await db.MonthlyAccountSummaries.ToListAsync();
        var summaryKeys = summaries.Select(s => SummaryKey(s.AccountId, s.Month)).ToHashSet();
        var tx = await db.FinanceTransactions.ToListAsync();
        foreach (var t in tx)
        {
            if (balances.ContainsKey(t.AccountId) && !HasSummaryFor(t.AccountId, t.OccurredOn, summaryKeys))
            {
                balances[t.AccountId] += t.Kind switch
                {
                    FinanceTransactionKind.Income => t.Amount,
                    FinanceTransactionKind.Dividend => t.Amount,
                    FinanceTransactionKind.Sell => t.Amount,
                    FinanceTransactionKind.Expense => -t.Amount,
                    FinanceTransactionKind.Buy => -t.Amount,
                    FinanceTransactionKind.Fee => -t.Amount,
                    FinanceTransactionKind.Transfer => -t.Amount,
                    _ => 0m
                };
            }
            if (t.Kind == FinanceTransactionKind.Transfer &&
                t.TransferAccountId.HasValue &&
                balances.ContainsKey(t.TransferAccountId.Value) &&
                !HasSummaryFor(t.TransferAccountId.Value, t.OccurredOn, summaryKeys))
            {
                balances[t.TransferAccountId.Value] += t.Amount;
            }
        }
        foreach (var s in summaries)
            if (balances.ContainsKey(s.AccountId))
                balances[s.AccountId] += s.Income - s.Expenses;
        foreach (var account in accounts) account.Balance = balances[account.Id];
        await db.SaveChangesAsync();
    }
}
