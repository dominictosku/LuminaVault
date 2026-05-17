using LuminaVault.Data;
using LuminaVault.Domain;
using Microsoft.EntityFrameworkCore;
using static LuminaVault.Endpoints.OdsParsing;
using static LuminaVault.Endpoints.OdsTargets;

namespace LuminaVault.Endpoints;

/// Per-entity import functions. Each is idempotent (skips rows that already exist
/// by lowercase name/key) and adds entities to the DbContext without saving — the
/// orchestrator calls SaveChangesAsync once at the end.
///
/// All return the number of *new* rows inserted, and append human-readable warnings
/// to the shared list for rows skipped due to missing FKs or invalid data.
internal static class OdsImport
{
    /// Signature the per-entity build callbacks pass to ImportByName. Return null to
    /// skip the row (the callback should also push a warning explaining why).
    internal delegate T? RowToEntity<T>(List<string> row, Dictionary<string, int> headers, string name, List<string> warnings)
        where T : class;

    /// Common skeleton for "deduplicate by lowercased name and append new rows" entity importers.
    /// Pulls existing rows once, indexes them case-insensitively, walks the sheet, and calls
    /// `build` for each new row; the build callback owns all entity-specific field mapping.
    private static async Task<int> ImportByName<T>(
        Dictionary<string, List<List<string>>> tables,
        AppDbContext db,
        List<string> warnings,
        string canonicalSheetName,
        DbSet<T> dbSet,
        Func<T, string> nameOf,
        string[] nameHeaderAliases,
        RowToEntity<T> build) where T : class
    {
        if (!TryGetByAlias(tables, canonicalSheetName, out var rows)) return 0;
        var (headers, data) = SplitHeader(rows);
        var existing = await dbSet.ToListAsync();
        var index = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in existing) index[nameOf(e)] = e;
        var count = 0;
        foreach (var row in data)
        {
            var name = Get(row, headers, nameHeaderAliases);
            if (string.IsNullOrWhiteSpace(name) || index.ContainsKey(name)) continue;
            var entity = build(row, headers, name, warnings);
            if (entity is null) continue;
            dbSet.Add(entity);
            index[nameOf(entity)] = entity;
            count++;
        }
        return count;
    }

    public static Task<int> Accounts(Dictionary<string, List<List<string>>> tables, AppDbContext db, List<string> warnings) =>
        ImportByName(tables, db, warnings, "Accounts", db.FinanceAccounts, a => a.Name, ["Name"],
            (row, headers, name, _) =>
            {
                var account = new FinanceAccount
                {
                    Name = name,
                    Institution = EmptyToNull(Get(row, headers, "Institution")),
                    Type = ParseEnum(Get(row, headers, "Type"), FinanceAccountType.Checking),
                    Currency = EmptyToNull(Get(row, headers, "Currency"))?.ToUpperInvariant() ?? "CHF",
                    StartingBalance = ParseDecimal(Get(row, headers, "Starting balance", "Start balance")),
                    Balance = ParseDecimal(Get(row, headers, "Balance")),
                    Color = EmptyToNull(Get(row, headers, "Color")) ?? "#7c3aed",
                    Notes = EmptyToNull(Get(row, headers, "Notes")),
                    IsArchived = ParseBool(Get(row, headers, "Archived")),
                };
                if (account.StartingBalance == 0) account.StartingBalance = account.Balance;
                return account;
            });

    public static async Task<int> Transactions(Dictionary<string, List<List<string>>> tables, AppDbContext db, List<string> warnings)
    {
        if (!TryGetByAlias(tables, "Transactions", out var rows)) return 0;

        var (headers, data) = SplitHeader(rows);
        var accounts = await db.FinanceAccounts.ToDictionaryAsync(a => a.Name.ToLowerInvariant());
        var financeCategories = await db.FinanceCategories.ToDictionaryAsync(c => c.Name.ToLowerInvariant());
        var count = 0;
        foreach (var row in data)
        {
            var accountName = Get(row, headers, "Account");
            if (string.IsNullOrWhiteSpace(accountName) || !accounts.TryGetValue(accountName.ToLowerInvariant(), out var account))
            {
                warnings.Add($"Skipped transaction without a matching account: {Get(row, headers, "Payee")}");
                continue;
            }

            FinanceAccount? transferAccount = null;
            var transferName = Get(row, headers, "Transfer account");
            if (!string.IsNullOrWhiteSpace(transferName))
                accounts.TryGetValue(transferName.ToLowerInvariant(), out transferAccount);

            var category = EmptyToNull(Get(row, headers, "Category")) ?? "General";
            if (!financeCategories.ContainsKey(category.ToLowerInvariant()))
            {
                var financeCategory = new FinanceCategory
                {
                    Name = category,
                    Color = "#7c3aed",
                    SortOrder = financeCategories.Count,
                };
                db.FinanceCategories.Add(financeCategory);
                financeCategories[financeCategory.Name.ToLowerInvariant()] = financeCategory;
            }

            var kind = ParseEnum(Get(row, headers, "Kind"), FinanceTransactionKind.Expense);
            var amount = Math.Abs(ParseDecimal(Get(row, headers, "Amount")));
            var transaction = new FinanceTransaction
            {
                AccountId = account.Id,
                TransferAccountId = transferAccount?.Id,
                Kind = kind,
                Status = ParseEnum(Get(row, headers, "Status"), FinanceTransactionStatus.Cleared),
                OccurredOn = ParseDate(Get(row, headers, "Date")) ?? DateTime.UtcNow.Date,
                Payee = EmptyToNull(Get(row, headers, "Payee")) ?? "Imported transaction",
                Category = category,
                Amount = amount,
                Description = EmptyToNull(Get(row, headers, "Description")),
                Notes = EmptyToNull(Get(row, headers, "Notes")),
                TagsCsv = EmptyToNull(Get(row, headers, "Tags")) ?? "",
                Symbol = EmptyToNull(Get(row, headers, "Symbol"))?.Trim().ToUpperInvariant(),
                Quantity = ParseNullableDecimal(Get(row, headers, "Quantity")),
                PricePerUnit = ParseNullableDecimal(Get(row, headers, "Price per unit", "PricePerUnit", "Unit price")),
            };

            var splits = ParseSplits(Get(row, headers, "Splits"));
            if (splits.Count > 0)
            {
                if (kind is not (FinanceTransactionKind.Income or FinanceTransactionKind.Expense))
                {
                    warnings.Add($"Ignored splits on non-income/expense transaction: {transaction.Payee}");
                }
                else
                {
                    var splitSum = splits.Sum(s => s.Amount);
                    if (Math.Abs(splitSum - amount) > 0.01m)
                    {
                        warnings.Add($"Splits for '{transaction.Payee}' sum to {splitSum:F2}, not {amount:F2} — kept as a single-category transaction.");
                    }
                    else
                    {
                        var i = 0;
                        foreach (var split in splits)
                        {
                            split.SortOrder = i++;
                            transaction.Splits.Add(split);
                        }
                    }
                }
            }

            db.FinanceTransactions.Add(transaction);
            count++;
        }
        return count;
    }

    /// Inverse of OdsExport.EncodeSplits: parses `Category=Amount[|Notes];…` into split rows.
    /// Bad rows are skipped silently — caller validates the sum against the transaction amount.
    static List<TransactionSplit> ParseSplits(string? value)
    {
        var result = new List<TransactionSplit>();
        if (string.IsNullOrWhiteSpace(value)) return result;
        foreach (var raw in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var part = raw.Trim();
            if (part.Length == 0) continue;
            var eq = part.IndexOf('=');
            if (eq < 1 || eq == part.Length - 1) continue;
            var category = part[..eq].Trim();
            var rest = part[(eq + 1)..].Trim();
            string? notes = null;
            var pipe = rest.IndexOf('|');
            if (pipe >= 0)
            {
                notes = EmptyToNull(rest[(pipe + 1)..]);
                rest = rest[..pipe].Trim();
            }
            var amount = ParseDecimal(rest);
            if (string.IsNullOrWhiteSpace(category) || amount <= 0) continue;
            result.Add(new TransactionSplit
            {
                Category = category,
                Amount = amount,
                Notes = notes,
            });
        }
        return result;
    }

    public static async Task<int> Holdings(Dictionary<string, List<List<string>>> tables, AppDbContext db, List<string> warnings)
    {
        if (!TryGetByAlias(tables, "Holdings", out var rows)) return 0;

        var (headers, data) = SplitHeader(rows);
        var accounts = await db.FinanceAccounts.ToDictionaryAsync(a => a.Name.ToLowerInvariant());
        var existing = await db.Holdings.ToListAsync();
        var byKey = existing.ToDictionary(
            h => $"{h.AccountId}:{h.Symbol.ToUpperInvariant()}",
            StringComparer.OrdinalIgnoreCase);
        var count = 0;

        foreach (var row in data)
        {
            var accountName = Get(row, headers, "Account", "Konto");
            var symbol = EmptyToNull(Get(row, headers, "Symbol"))?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(accountName) || !accounts.TryGetValue(accountName.ToLowerInvariant(), out var account))
            {
                warnings.Add($"Skipped holding without a matching account: {symbol ?? "(missing symbol)"}");
                continue;
            }
            if (string.IsNullOrWhiteSpace(symbol))
            {
                warnings.Add($"Skipped holding without a symbol for account: {account.Name}");
                continue;
            }

            var key = $"{account.Id}:{symbol}";
            if (!byKey.TryGetValue(key, out var holding))
            {
                holding = new Holding
                {
                    AccountId = account.Id,
                    Symbol = symbol,
                };
                db.Holdings.Add(holding);
                byKey[key] = holding;
                count++;
            }

            var quantity = ParseNullableDecimal(Get(row, headers, "Quantity"));
            var averageCost = ParseNullableDecimal(Get(row, headers, "Average cost", "AverageCost"));
            if (quantity.HasValue && holding.Quantity == 0) holding.Quantity = quantity.Value;
            if (averageCost.HasValue && holding.AverageCost == 0) holding.AverageCost = averageCost.Value;
            holding.Name = EmptyToNull(Get(row, headers, "Name")) ?? holding.Name;
            holding.LastPrice = ParseNullableDecimal(Get(row, headers, "Last price", "LastPrice")) ?? holding.LastPrice;
            holding.LastPriceAt = ParseDate(Get(row, headers, "Last price at", "LastPriceAt")) ?? holding.LastPriceAt;
            holding.ProviderId = EmptyToNull(Get(row, headers, "Provider ID", "ProviderId")) ?? holding.ProviderId;
            holding.Notes = EmptyToNull(Get(row, headers, "Notes", "Notizen")) ?? holding.Notes;
            holding.UpdatedAt = DateTime.UtcNow;
        }

        return count;
    }

    public static async Task<int> MonthlySummaries(Dictionary<string, List<List<string>>> tables, AppDbContext db, List<string> warnings)
    {
        if (!TryGetByAlias(tables, "Monthly summaries", out var rows)) return 0;

        var (headers, data) = SplitHeader(rows);
        var accounts = await db.FinanceAccounts.ToDictionaryAsync(a => a.Name.ToLowerInvariant());
        var existing = await db.MonthlyAccountSummaries
            .Select(s => new { s.AccountId, s.Month })
            .ToListAsync();
        var existingKeys = existing.Select(s => SummaryKey(s.AccountId, s.Month)).ToHashSet();
        var count = 0;

        foreach (var row in data)
        {
            var accountName = Get(row, headers, "Account", "Konto");
            if (string.IsNullOrWhiteSpace(accountName) || !accounts.TryGetValue(accountName.ToLowerInvariant(), out var account))
            {
                warnings.Add($"Skipped monthly summary without a matching account: {accountName}");
                continue;
            }

            var month = MonthStart(ParseDate(Get(row, headers, "Month", "Monat")) ?? DateTime.UtcNow.Date);
            var key = SummaryKey(account.Id, month);
            if (existingKeys.Contains(key)) continue;

            db.MonthlyAccountSummaries.Add(new MonthlyAccountSummary
            {
                AccountId = account.Id,
                Month = month,
                Income = Math.Abs(ParseDecimal(Get(row, headers, "Income", "Einnahmen", "Gutschrift"))),
                Expenses = Math.Abs(ParseDecimal(Get(row, headers, "Expenses", "Ausgaben", "Belastung"))),
                OpeningBalance = ParseNullableDecimal(Get(row, headers, "Opening balance", "Start balance")),
                ClosingBalance = ParseNullableDecimal(Get(row, headers, "Closing balance", "End balance", "Vermögen")),
                Notes = EmptyToNull(Get(row, headers, "Notes", "Notizen")),
            });
            existingKeys.Add(key);
            count++;
        }
        return count;
    }

    public static async Task<int> Subscriptions(Dictionary<string, List<List<string>>> tables, AppDbContext db, List<string> warnings)
    {
        // `isBudgetSheet` preserves a historical quirk: when the sheet was named "Abos"
        // (the legacy German budget sheet), the importer forces AutoRenew=true regardless
        // of the cell value, because old sheets didn't include the column at all.
        var isBudgetSheet = !tables.ContainsKey("Subscriptions") && tables.ContainsKey("Abos");
        var accounts = await db.FinanceAccounts.ToDictionaryAsync(a => a.Name.ToLowerInvariant());
        var financeCategories = await db.FinanceCategories.ToDictionaryAsync(c => c.Name.ToLowerInvariant());

        return await ImportByName(tables, db, warnings, "Subscriptions", db.Subscriptions, s => s.Name,
            ["Name", "Leistung"],
            (row, headers, name, w) =>
            {
                var accountName = Get(row, headers, "Account", "Konto");
                accounts.TryGetValue((accountName ?? "").ToLowerInvariant(), out var account);
                var category = EmptyToNull(Get(row, headers, "Category", "Kategorie")) ?? "Subscriptions";
                if (!financeCategories.ContainsKey(category.ToLowerInvariant()))
                {
                    var financeCategory = new FinanceCategory
                    {
                        Name = category,
                        Color = "#7c3aed",
                        SortOrder = financeCategories.Count,
                    };
                    db.FinanceCategories.Add(financeCategory);
                    financeCategories[financeCategory.Name.ToLowerInvariant()] = financeCategory;
                }

                var interval = ParseDecimal(Get(row, headers, "Interval days", "Intervall in Tagen"));
                var unitRaw = Get(row, headers, "Interval unit");
                var countRaw = Get(row, headers, "Interval count");
                // Prefer the unit+count columns from new exports; fall back to the legacy
                // "Interval days" column so older spreadsheets keep importing unchanged.
                BillingIntervalUnit intervalUnit;
                int intervalCount;
                if (!string.IsNullOrWhiteSpace(unitRaw))
                {
                    intervalUnit = ParseEnum(unitRaw, BillingIntervalUnit.Month);
                    intervalCount = Math.Max(1, (int)Math.Round(ParseDecimal(string.IsNullOrWhiteSpace(countRaw) ? "1" : countRaw)));
                }
                else
                {
                    intervalUnit = BillingIntervalUnit.Day;
                    intervalCount = Math.Max(1, (int)Math.Round(interval == 0 ? 30 : interval));
                }
                var subscription = new Subscription
                {
                    Name = name,
                    Category = category,
                    Provider = EmptyToNull(Get(row, headers, "Provider")),
                    AccountId = account?.Id,
                    Amount = Math.Abs(ParseDecimal(Get(row, headers, "Amount", "Preis"))),
                    Currency = EmptyToNull(Get(row, headers, "Currency")) ?? "CHF",
                    BillingIntervalUnit = intervalUnit,
                    BillingIntervalCount = intervalCount,
                    BillingIntervalDays = FinanceHelpers.BillingPeriodInDays(intervalUnit, intervalCount),
                    StartedOn = ParseDate(Get(row, headers, "Started on", "Startdatum")) ?? DateTime.UtcNow.Date,
                    NextDueOn = ParseDate(Get(row, headers, "Next due", "Nächstes Fälligkeitsdatum")) ?? DateTime.UtcNow.Date,
                    AutoRenew = ParseBool(Get(row, headers, "Auto renew")) || isBudgetSheet,
                    Status = ParseEnum(Get(row, headers, "Status"), SubscriptionStatus.Active),
                    Notes = EmptyToNull(Get(row, headers, "Notes", "Notiz")),
                };
                if (subscription.Amount <= 0)
                {
                    w.Add($"Skipped subscription without price: {name}");
                    return null;
                }
                return subscription;
            });
    }

    public static Task<int> FinanceCategories(Dictionary<string, List<List<string>>> tables, AppDbContext db, List<string> warnings) =>
        ImportByName(tables, db, warnings, "Finance categories", db.FinanceCategories, c => c.Name,
            ["Name", "Category", "Kategorie"],
            (row, headers, name, _) => new FinanceCategory
            {
                Name = name.Trim(),
                Color = EmptyToNull(Get(row, headers, "Color", "Farbe")) ?? "#7c3aed",
                SortOrder = (int)Math.Round(ParseDecimal(Get(row, headers, "Sort order", "Sortierung"))),
            });

    public static Task<int> AssetCategories(Dictionary<string, List<List<string>>> tables, AppDbContext db, List<string> warnings) =>
        ImportByName(tables, db, warnings, "Asset categories", db.AssetCategories, c => c.Name,
            ["Name", "Category", "Kategorie"],
            (row, headers, name, _) => new AssetCategory
            {
                Name = name.Trim(),
                Color = EmptyToNull(Get(row, headers, "Color", "Farbe")) ?? "#7c3aed",
                SortOrder = (int)Math.Round(ParseDecimal(Get(row, headers, "Sort order", "Sortierung"))),
            });

    public static async Task<int> Loans(Dictionary<string, List<List<string>>> tables, AppDbContext db, List<string> warnings)
    {
        var accounts = await db.FinanceAccounts.ToDictionaryAsync(a => a.Name.ToLowerInvariant());
        return await ImportByName(tables, db, warnings, "Loans", db.Loans, l => l.Name, ["Name"],
            (row, headers, name, w) =>
            {
                FinanceAccount? account = null;
                var accountName = Get(row, headers, "Account");
                if (!string.IsNullOrWhiteSpace(accountName))
                    accounts.TryGetValue(accountName.ToLowerInvariant(), out account);

                var principal = Math.Abs(ParseDecimal(Get(row, headers, "Principal")));
                if (principal <= 0)
                {
                    w.Add($"Skipped loan with non-positive principal: {name}");
                    return null;
                }
                var termRaw = ParseDecimal(Get(row, headers, "Term months", "Term"));
                var term = (int)Math.Round(termRaw <= 0 ? 1 : termRaw);
                return new Loan
                {
                    Name = name.Trim(),
                    Lender = EmptyToNull(Get(row, headers, "Lender")),
                    AccountId = account?.Id,
                    Currency = (EmptyToNull(Get(row, headers, "Currency")) ?? "CHF").ToUpperInvariant(),
                    Principal = principal,
                    AnnualInterestRate = Math.Max(0m, ParseDecimal(Get(row, headers, "Annual interest rate", "Interest rate", "Rate"))),
                    TermMonths = term,
                    StartDate = ParseDate(Get(row, headers, "Start date", "Started on")) ?? DateTime.UtcNow.Date,
                    ExtraMonthlyPayment = Math.Max(0m, ParseDecimal(Get(row, headers, "Extra monthly payment", "Extra payment"))),
                    Status = ParseEnum(Get(row, headers, "Status"), LoanStatus.Active),
                    Notes = EmptyToNull(Get(row, headers, "Notes")),
                };
            });
    }

    public static async Task<int> Assets(Dictionary<string, List<List<string>>> tables, AppDbContext db, List<string> warnings)
    {
        var categories = await db.AssetCategories.ToDictionaryAsync(c => c.Name.ToLowerInvariant());
        return await ImportByName(tables, db, warnings, "Assets", db.Items, i => i.Name,
            ["Name", "Bezeichnung"],
            (row, headers, name, _) =>
            {
                var category = Get(row, headers, "Category", "Kategorie");
                if (!string.IsNullOrWhiteSpace(category) && !categories.ContainsKey(category.ToLowerInvariant()))
                {
                    var assetCategory = new AssetCategory
                    {
                        Name = category.Trim(),
                        Color = "#7c3aed",
                        SortOrder = categories.Count,
                    };
                    db.AssetCategories.Add(assetCategory);
                    categories[assetCategory.Name.ToLowerInvariant()] = assetCategory;
                }
                var tags = EmptyToNull(Get(row, headers, "Tags")) ?? category ?? "";
                var quantityRaw = ParseDecimal(Get(row, headers, "Quantity"));
                return new Item
                {
                    Name = name,
                    Category = EmptyToNull(category),
                    Description = EmptyToNull(Get(row, headers, "Description")),
                    Brand = EmptyToNull(Get(row, headers, "Brand")),
                    Model = EmptyToNull(Get(row, headers, "Model")),
                    SerialNumber = EmptyToNull(Get(row, headers, "Serial number", "Seriennummer")),
                    Value = ParseNullableDecimal(Get(row, headers, "Value", "Kosten")),
                    PurchaseDate = ParseDate(Get(row, headers, "Purchase date", "Kaufdatum")),
                    WarrantyUntil = ParseDate(Get(row, headers, "Warranty until")),
                    Quantity = Math.Max(1, (int)Math.Round(quantityRaw == 0 ? 1 : quantityRaw)),
                    Notes = EmptyToNull(Get(row, headers, "Notes", "Notizen")),
                    TagsCsv = tags,
                };
            });
    }

    /// Builds an artificial single-sheet table dictionary for the mapped-import flow:
    /// the user picks which source columns map to canonical headers, and we re-emit
    /// the sheet with renamed headers so the regular import functions just work.
    public static List<List<string>> BuildMappedTable(List<List<string>> rows, Dictionary<string, string> columns)
    {
        var (headers, data) = SplitHeader(rows);
        var canonicalHeaders = columns.Keys.Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
        var mapped = new List<List<string>> { canonicalHeaders };
        foreach (var row in data)
        {
            mapped.Add(canonicalHeaders.Select(canonical =>
            {
                var source = columns[canonical];
                if (string.IsNullOrWhiteSpace(source)) return "";
                return Get(row, headers, source) ?? "";
            }).ToList());
        }
        return mapped;
    }
}
