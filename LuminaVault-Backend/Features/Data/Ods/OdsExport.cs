using LuminaVault.Domain;
using static LuminaVault.Endpoints.OdsWriter;

namespace LuminaVault.Endpoints;

/// Per-entity export mappers: entity collection → OdsSheet. The endpoint orchestrator
/// loads each collection from the DB and calls these to assemble the workbook.
internal static class OdsExport
{
    public static OdsSheet Accounts(IEnumerable<FinanceAccount> accounts) =>
        Sheet("Accounts",
            new[] { Row("Name", "Institution", "Type", "Currency", "Starting balance", "Balance", "Color", "Notes", "Archived") }
                .Concat(accounts.Select(a => Row(
                    a.Name, a.Institution, a.Type, a.Currency, a.StartingBalance, a.Balance, a.Color, a.Notes, a.IsArchived))));

    public static OdsSheet Transactions(IEnumerable<FinanceTransaction> transactions) =>
        Sheet("Transactions",
            new[] { Row("Date", "Kind", "Account", "Transfer account", "Payee", "Category", "Amount", "Status", "Description", "Notes", "Tags", "Symbol", "Quantity", "Price per unit") }
                .Concat(transactions.Select(t => Row(
                    DateOnly.FromDateTime(t.OccurredOn), t.Kind, t.Account?.Name, t.TransferAccount?.Name,
                    t.Payee, t.Category, t.Amount, t.Status, t.Description, t.Notes, t.TagsCsv,
                    t.Symbol, t.Quantity, t.PricePerUnit))));

    public static OdsSheet Holdings(IEnumerable<Holding> holdings) =>
        Sheet("Holdings",
            new[] { Row("Account", "Symbol", "Name", "Quantity", "Average cost", "Last price", "Last price at", "Provider ID", "Notes") }
                .Concat(holdings.Select(h => Row(
                    h.Account?.Name, h.Symbol, h.Name, h.Quantity, h.AverageCost, h.LastPrice,
                    h.LastPriceAt.HasValue ? DateOnly.FromDateTime(h.LastPriceAt.Value) : null,
                    h.ProviderId, h.Notes))));

    public static OdsSheet MonthlySummaries(IEnumerable<MonthlyAccountSummary> summaries) =>
        Sheet("Monthly summaries",
            new[] { Row("Month", "Account", "Income", "Expenses", "Opening balance", "Closing balance", "Notes") }
                .Concat(summaries.Select(s => Row(
                    DateOnly.FromDateTime(s.Month), s.Account?.Name, s.Income, s.Expenses,
                    s.OpeningBalance, s.ClosingBalance, s.Notes))));

    public static OdsSheet Budgets(IEnumerable<FinanceBudget> budgets) =>
        Sheet("Budgets",
            new[] { Row("Month", "Category", "Limit amount", "Notes") }
                .Concat(budgets.Select(b => Row(
                    DateOnly.FromDateTime(b.Month), b.Category, b.LimitAmount, b.Notes))));

    public static OdsSheet BalanceSnapshots(IEnumerable<AccountBalanceSnapshot> snapshots) =>
        Sheet("Balance snapshots",
            new[] { Row("Date", "Account", "Actual balance", "Expected balance", "Difference", "Reconciled", "Notes") }
                .Concat(snapshots.Select(s => Row(
                    DateOnly.FromDateTime(s.SnapshotDate), s.Account?.Name, s.ActualBalance,
                    s.ExpectedBalance, s.Difference, s.IsReconciled, s.Notes))));

    public static OdsSheet Subscriptions(IEnumerable<Subscription> subscriptions) =>
        Sheet("Subscriptions",
            new[] { Row("Name", "Category", "Provider", "Account", "Amount", "Currency", "Interval days", "Started on", "Next due", "Auto renew", "Status", "Notes") }
                .Concat(subscriptions.Select(s => Row(
                    s.Name, s.Category, s.Provider, s.Account?.Name, s.Amount, s.Currency,
                    s.BillingIntervalDays, DateOnly.FromDateTime(s.StartedOn), DateOnly.FromDateTime(s.NextDueOn),
                    s.AutoRenew, s.Status, s.Notes))));

    public static OdsSheet AssetCategories(IEnumerable<AssetCategory> categories) =>
        Sheet("Asset categories",
            new[] { Row("Name", "Color", "Sort order") }
                .Concat(categories.Select(c => Row(c.Name, c.Color, c.SortOrder))));

    public static OdsSheet FinanceCategories(IEnumerable<FinanceCategory> categories) =>
        Sheet("Finance categories",
            new[] { Row("Name", "Color", "Sort order") }
                .Concat(categories.Select(c => Row(c.Name, c.Color, c.SortOrder))));

    public static OdsSheet Assets(IEnumerable<Item> assets) =>
        Sheet("Assets",
            new[] { Row("Name", "Category", "Description", "Brand", "Model", "Serial number", "Value", "Purchase date", "Warranty until", "Quantity", "Notes", "Tags") }
                .Concat(assets.Select(i => Row(
                    i.Name, i.Category, i.Description, i.Brand, i.Model, i.SerialNumber, i.Value,
                    i.PurchaseDate.HasValue ? DateOnly.FromDateTime(i.PurchaseDate.Value) : null,
                    i.WarrantyUntil.HasValue ? DateOnly.FromDateTime(i.WarrantyUntil.Value) : null,
                    i.Quantity, i.Notes, i.TagsCsv))));
}
