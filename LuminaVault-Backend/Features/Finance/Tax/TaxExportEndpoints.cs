using LuminaVault.Data;
using LuminaVault.Domain;
using Microsoft.EntityFrameworkCore;
using static LuminaVault.Endpoints.FinanceHelpers;
using static LuminaVault.Endpoints.OdsWriter;

namespace LuminaVault.Endpoints;

/// Swiss tax-export bundle. Produces year-end snapshots structured the way the
/// Wertschriftenverzeichnis and wealth declaration want them: per-security holdings
/// on Dec 31, cash account balances on Dec 31, and income/expense aggregates for
/// the tax year.
///
/// Prices are the holding's current `LastPrice` (we don't keep historical price
/// history) — each row carries a `PriceIsStale` flag the spreadsheet renders as a
/// warning column so the user knows which rows to override before filing.
public record TaxHoldingRow(
    string Account,
    string Symbol,
    string? Name,
    decimal Quantity,
    decimal AverageCost,
    decimal CostBasis,
    decimal? PriceAtYearEnd,
    DateTime? PriceAsOf,
    bool PriceIsStale,
    decimal? EstimatedValue,
    string Currency);

public record TaxAccountBalanceRow(
    string Account,
    string Currency,
    string Type,
    decimal BalanceAtYearEnd);

public record TaxCategoryTotalRow(
    string Category,
    decimal Amount);

public record TaxExportDto(
    int Year,
    DateTime AsOf,
    TaxHoldingRow[] Holdings,
    decimal TotalSecuritiesValue,
    TaxAccountBalanceRow[] AccountBalances,
    decimal TotalCashBalance,
    TaxCategoryTotalRow[] Income,
    decimal TotalIncome,
    TaxCategoryTotalRow[] Expenses,
    decimal TotalExpenses,
    string[] Warnings);

internal static class TaxExportEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var tax = app.MapGroup("/api/finance/tax-export").RequireAuthorization().WithTags("Finance");

        tax.MapGet("/", async (AppDbContext db, int? year) =>
        {
            var bundle = await BuildBundle(db, year ?? DateTime.UtcNow.Year - 1);
            return Results.Ok(bundle);
        });

        tax.MapGet("/ods", async (AppDbContext db, int? year) =>
        {
            var resolved = year ?? DateTime.UtcNow.Year - 1;
            var bundle = await BuildBundle(db, resolved);
            var bytes = BuildOds(bundle);
            return Results.File(bytes, "application/vnd.oasis.opendocument.spreadsheet",
                $"LuminaVault-tax-{resolved}.ods");
        });
    }

    public static async Task<TaxExportDto> BuildBundle(AppDbContext db, int year)
    {
        var yearEnd = new DateTime(year, 12, 31);
        var yearStart = new DateTime(year, 1, 1);
        var nextYear = yearStart.AddYears(1);
        var warnings = new List<string>();

        var accounts = await db.FinanceAccounts.OrderBy(a => a.Name).ToListAsync();

        var holdings = await BuildYearEndHoldings(db, accounts, yearEnd, warnings);
        var balances = await BuildAccountBalances(db, accounts, yearEnd);
        var (income, totalIncome) = await BuildCategoryTotals(db, FinanceTransactionKind.Income, yearStart, nextYear);
        var (expenses, totalExpenses) = await BuildCategoryTotals(db, FinanceTransactionKind.Expense, yearStart, nextYear);

        return new TaxExportDto(
            year, yearEnd,
            holdings, holdings.Sum(h => h.EstimatedValue ?? 0m),
            balances, balances.Sum(b => b.BalanceAtYearEnd),
            income, totalIncome,
            expenses, totalExpenses,
            warnings.ToArray());
    }

    /// Replays Buy/Sell transactions for each (account, symbol) up to Dec 31 of the
    /// requested year — same average-cost rolling math as RecalculateHoldings, but
    /// frozen at a date. Symbols sold to zero and never reopened are dropped.
    static async Task<TaxHoldingRow[]> BuildYearEndHoldings(
        AppDbContext db,
        List<FinanceAccount> accounts,
        DateTime yearEnd,
        List<string> warnings)
    {
        var accountsById = accounts.ToDictionary(a => a.Id);
        var trades = await db.FinanceTransactions
            .Where(t => t.OccurredOn <= yearEnd)
            .Where(t => t.Kind == FinanceTransactionKind.Buy || t.Kind == FinanceTransactionKind.Sell)
            .Where(t => t.Status != FinanceTransactionStatus.Pending)
            .Where(t => t.Symbol != null && t.Symbol != "")
            .OrderBy(t => t.OccurredOn).ThenBy(t => t.Id)
            .ToListAsync();
        var holdings = await db.Holdings.Include(h => h.Account).ToListAsync();
        var holdingByKey = holdings.ToDictionary(h => Key(h.AccountId, h.Symbol), StringComparer.OrdinalIgnoreCase);

        var rows = new List<TaxHoldingRow>();
        foreach (var group in trades.GroupBy(t => new { t.AccountId, Symbol = t.Symbol! }))
        {
            decimal qty = 0m, avgCost = 0m;
            foreach (var t in group.OrderBy(t => t.OccurredOn).ThenBy(t => t.Id))
            {
                var tradeQty = t.Quantity ?? 0m;
                var tradePrice = t.PricePerUnit ?? 0m;
                if (tradeQty <= 0) continue;
                if (t.Kind == FinanceTransactionKind.Buy)
                {
                    var newQty = qty + tradeQty;
                    avgCost = newQty > 0 ? (qty * avgCost + tradeQty * tradePrice) / newQty : 0m;
                    qty = newQty;
                }
                else
                {
                    qty -= tradeQty;
                    if (qty <= 0) { qty = 0m; avgCost = 0m; }
                }
            }
            if (qty <= 0) continue;

            var account = accountsById.GetValueOrDefault(group.Key.AccountId);
            var holding = holdingByKey.GetValueOrDefault(Key(group.Key.AccountId, group.Key.Symbol));
            var lastPrice = holding?.LastPrice;
            var priceDate = holding?.LastPriceAt;
            var stale = !priceDate.HasValue || priceDate.Value.Date < yearEnd.Date;
            if (stale && lastPrice.HasValue)
                warnings.Add($"{group.Key.Symbol}: using last known price from {priceDate?.ToString("yyyy-MM-dd") ?? "an unknown date"} — verify against the Dec 31 quote before filing.");
            if (!lastPrice.HasValue)
                warnings.Add($"{group.Key.Symbol}: no last price recorded — value cell left blank.");

            rows.Add(new TaxHoldingRow(
                Account: account?.Name ?? "(unknown)",
                Symbol: group.Key.Symbol,
                Name: holding?.Name,
                Quantity: qty,
                AverageCost: avgCost,
                CostBasis: Math.Round(qty * avgCost, 2),
                PriceAtYearEnd: lastPrice,
                PriceAsOf: priceDate,
                PriceIsStale: stale,
                EstimatedValue: lastPrice.HasValue ? Math.Round(qty * lastPrice.Value, 2) : null,
                Currency: account?.Currency ?? "CHF"));
        }
        return rows
            .OrderBy(r => r.Account)
            .ThenBy(r => r.Symbol)
            .ToArray();
    }

    static async Task<TaxAccountBalanceRow[]> BuildAccountBalances(
        AppDbContext db,
        List<FinanceAccount> accounts,
        DateTime yearEnd)
    {
        var rows = new List<TaxAccountBalanceRow>();
        foreach (var account in accounts.Where(a => !a.IsArchived))
        {
            var balance = await ExpectedBalanceAt(db, account.Id, yearEnd);
            rows.Add(new TaxAccountBalanceRow(
                account.Name, account.Currency, account.Type.ToString(),
                Math.Round(balance, 2)));
        }
        return rows
            .OrderBy(r => r.Type)
            .ThenBy(r => r.Account)
            .ToArray();
    }

    static async Task<(TaxCategoryTotalRow[] Rows, decimal Total)> BuildCategoryTotals(
        AppDbContext db,
        FinanceTransactionKind kind,
        DateTime from,
        DateTime toExclusive)
    {
        var tx = await db.FinanceTransactions
            .Include(t => t.Splits)
            .Where(t => t.Kind == kind)
            .Where(t => t.OccurredOn >= from && t.OccurredOn < toExclusive)
            .Where(t => t.Status != FinanceTransactionStatus.Pending)
            .ToListAsync();
        var grouped = tx
            .SelectMany(ExpandCategoryAmounts)
            .GroupBy(x => x.Category)
            .Select(g => new TaxCategoryTotalRow(g.Key, Math.Round(g.Sum(x => x.Amount), 2)))
            .OrderByDescending(r => r.Amount)
            .ToArray();
        return (grouped, grouped.Sum(r => r.Amount));
    }

    static byte[] BuildOds(TaxExportDto bundle)
    {
        var holdings = Sheet($"Year-end holdings {bundle.Year}",
            new[] { Row("Account", "Symbol", "Name", "Quantity", "Average cost",
                "Cost basis", "Price at year-end", "Price as of",
                "Price is stale", "Estimated value", "Currency") }
                .Concat(bundle.Holdings.Select(h => Row(
                    h.Account, h.Symbol, h.Name, h.Quantity, h.AverageCost, h.CostBasis,
                    h.PriceAtYearEnd,
                    h.PriceAsOf.HasValue ? DateOnly.FromDateTime(h.PriceAsOf.Value) : null,
                    h.PriceIsStale, h.EstimatedValue, h.Currency))));

        var balances = Sheet($"Account balances {bundle.Year}",
            new[] { Row("Account", "Type", "Currency", "Balance at year-end") }
                .Concat(bundle.AccountBalances.Select(b => Row(
                    b.Account, b.Type, b.Currency, b.BalanceAtYearEnd))));

        var income = Sheet($"Income {bundle.Year}",
            new[] { Row("Category", "Amount") }
                .Concat(bundle.Income.Select(i => Row(i.Category, i.Amount))));

        var expenses = Sheet($"Expenses {bundle.Year}",
            new[] { Row("Category", "Amount") }
                .Concat(bundle.Expenses.Select(e => Row(e.Category, e.Amount))));

        var summary = Sheet($"Summary {bundle.Year}",
            new object?[][]
            {
                Row("As of", DateOnly.FromDateTime(bundle.AsOf)),
                Row("Total securities value", bundle.TotalSecuritiesValue),
                Row("Total cash balance", bundle.TotalCashBalance),
                Row("Total income", bundle.TotalIncome),
                Row("Total expenses", bundle.TotalExpenses),
                Row("Net income (income − expenses)", bundle.TotalIncome - bundle.TotalExpenses),
                Row(),
                Row("Warnings"),
            }.Concat(bundle.Warnings.Select(w => Row(w))));

        return OdsWriter.Build(new[] { summary, holdings, balances, income, expenses });
    }

    static string Key(int accountId, string symbol) => $"{accountId}:{symbol.ToUpperInvariant()}";
}
