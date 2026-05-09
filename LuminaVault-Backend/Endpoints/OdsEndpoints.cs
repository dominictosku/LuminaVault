using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Xml.Linq;
using LuminaVault.Data;
using LuminaVault.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

public record OdsImportResult(
    int Accounts,
    int Transactions,
    int Subscriptions,
    int Assets,
    string[] Warnings);

public static class OdsEndpoints
{
    static readonly XNamespace TableNs = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    static readonly XNamespace TextNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    static readonly XNamespace OfficeNs = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";

    public static IEndpointRouteBuilder MapOdsData(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/data").RequireAuthorization().WithTags("Data");

        g.MapGet("/export/ods", async (AppDbContext db) =>
        {
            await RecalculateFinanceBalances(db);
            var accounts = await db.FinanceAccounts.OrderBy(a => a.Name).ToListAsync();
            var transactions = await db.FinanceTransactions
                .Include(t => t.Account)
                .Include(t => t.TransferAccount)
                .OrderByDescending(t => t.OccurredOn)
                .ThenByDescending(t => t.Id)
                .ToListAsync();
            var subscriptions = await db.Subscriptions
                .Include(s => s.Account)
                .OrderBy(s => s.NextDueOn)
                .ToListAsync();
            var assets = await db.Items.OrderBy(i => i.Name).ToListAsync();

            var bytes = BuildOds(new[]
            {
                Sheet("Accounts", new[] { Row("Name", "Institution", "Type", "Currency", "Starting balance", "Balance", "Color", "Notes", "Archived") }
                    .Concat(accounts.Select(a => Row(a.Name, a.Institution, a.Type, a.Currency, a.StartingBalance, a.Balance, a.Color, a.Notes, a.IsArchived)))),
                Sheet("Transactions", new[] { Row("Date", "Kind", "Account", "Transfer account", "Payee", "Category", "Amount", "Status", "Description", "Notes", "Tags") }
                    .Concat(transactions.Select(t => Row(DateOnly.FromDateTime(t.OccurredOn), t.Kind, t.Account?.Name, t.TransferAccount?.Name,
                        t.Payee, t.Category, t.Amount, t.Status, t.Description, t.Notes, t.TagsCsv))
                    )),
                Sheet("Subscriptions", new[] { Row("Name", "Category", "Provider", "Account", "Amount", "Currency", "Interval days", "Started on", "Next due", "Auto renew", "Status", "Notes") }
                    .Concat(subscriptions.Select(s => Row(s.Name, s.Category, s.Provider, s.Account?.Name, s.Amount, s.Currency,
                        s.BillingIntervalDays, DateOnly.FromDateTime(s.StartedOn), DateOnly.FromDateTime(s.NextDueOn),
                        s.AutoRenew, s.Status, s.Notes))
                    )),
                Sheet("Assets", new[] { Row("Name", "Category", "Description", "Brand", "Model", "Serial number", "Value", "Purchase date", "Warranty until", "Quantity", "Notes", "Tags") }
                    .Concat(assets.Select(i => Row(i.Name, FirstTag(i.TagsCsv), i.Description, i.Brand, i.Model, i.SerialNumber, i.Value,
                        i.PurchaseDate.HasValue ? DateOnly.FromDateTime(i.PurchaseDate.Value) : null,
                        i.WarrantyUntil.HasValue ? DateOnly.FromDateTime(i.WarrantyUntil.Value) : null,
                        i.Quantity, i.Notes, i.TagsCsv))
                    ))
            });

            var fileName = $"LuminaVault-export-{DateTime.UtcNow:yyyy-MM-dd}.ods";
            return Results.File(bytes, "application/vnd.oasis.opendocument.spreadsheet", fileName);
        });

        g.MapPost("/import/ods", async ([FromForm] IFormFile file, AppDbContext db) =>
        {
            if (file.Length == 0) return Results.BadRequest(new { error = "Choose an ODS file." });
            if (!Path.GetExtension(file.FileName).Equals(".ods", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = "Only .ods files are supported." });

            await using var stream = file.OpenReadStream();
            var tables = ReadOdsTables(stream);
            var warnings = new List<string>();

            var accounts = await ImportAccounts(tables, db, warnings);
            await db.SaveChangesAsync();
            var subscriptions = await ImportSubscriptions(tables, db, warnings);
            var assets = await ImportAssets(tables, db, warnings);
            var transactions = await ImportTransactions(tables, db, warnings);
            await db.SaveChangesAsync();
            await RecalculateFinanceBalances(db);

            return Results.Ok(new OdsImportResult(accounts, transactions, subscriptions, assets, warnings.ToArray()));
        }).DisableAntiforgery();

        return app;
    }

    static async Task<int> ImportAccounts(Dictionary<string, List<List<string>>> tables, AppDbContext db, List<string> warnings)
    {
        if (!TryGetTable(tables, "Accounts", out var rows) && !TryGetTable(tables, "Konten", out rows))
            return 0;

        var (headers, data) = SplitHeader(rows);
        var existing = await db.FinanceAccounts.ToDictionaryAsync(a => a.Name.ToLowerInvariant());
        var count = 0;
        foreach (var row in data)
        {
            var name = Get(row, headers, "Name");
            if (string.IsNullOrWhiteSpace(name) || existing.ContainsKey(name.ToLowerInvariant())) continue;
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
            db.FinanceAccounts.Add(account);
            existing[account.Name.ToLowerInvariant()] = account;
            count++;
        }
        return count;
    }

    static async Task<int> ImportTransactions(Dictionary<string, List<List<string>>> tables, AppDbContext db, List<string> warnings)
    {
        if (!TryGetTable(tables, "Transactions", out var rows))
            return 0;

        var (headers, data) = SplitHeader(rows);
        var accounts = await db.FinanceAccounts.ToDictionaryAsync(a => a.Name.ToLowerInvariant());
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

            db.FinanceTransactions.Add(new FinanceTransaction
            {
                AccountId = account.Id,
                TransferAccountId = transferAccount?.Id,
                Kind = ParseEnum(Get(row, headers, "Kind"), FinanceTransactionKind.Expense),
                Status = ParseEnum(Get(row, headers, "Status"), FinanceTransactionStatus.Cleared),
                OccurredOn = ParseDate(Get(row, headers, "Date")) ?? DateTime.UtcNow.Date,
                Payee = EmptyToNull(Get(row, headers, "Payee")) ?? "Imported transaction",
                Category = EmptyToNull(Get(row, headers, "Category")) ?? "General",
                Amount = Math.Abs(ParseDecimal(Get(row, headers, "Amount"))),
                Description = EmptyToNull(Get(row, headers, "Description")),
                Notes = EmptyToNull(Get(row, headers, "Notes")),
                TagsCsv = EmptyToNull(Get(row, headers, "Tags")) ?? "",
            });
            count++;
        }
        return count;
    }

    static async Task<int> ImportSubscriptions(Dictionary<string, List<List<string>>> tables, AppDbContext db, List<string> warnings)
    {
        var isBudgetSheet = false;
        if (!TryGetTable(tables, "Subscriptions", out var rows))
        {
            isBudgetSheet = TryGetTable(tables, "Abos", out rows);
            if (!isBudgetSheet) return 0;
        }

        var (headers, data) = SplitHeader(rows);
        var accounts = await db.FinanceAccounts.ToDictionaryAsync(a => a.Name.ToLowerInvariant());
        var existing = await db.Subscriptions.ToDictionaryAsync(s => s.Name.ToLowerInvariant());
        var count = 0;
        foreach (var row in data)
        {
            var name = Get(row, headers, "Name", "Leistung");
            if (string.IsNullOrWhiteSpace(name) || existing.ContainsKey(name.ToLowerInvariant())) continue;

            var accountName = Get(row, headers, "Account", "Konto");
            accounts.TryGetValue((accountName ?? "").ToLowerInvariant(), out var account);

            var interval = ParseDecimal(Get(row, headers, "Interval days", "Intervall in Tagen"));
            var subscription = new Subscription
            {
                Name = name,
                Category = EmptyToNull(Get(row, headers, "Category", "Kategorie")) ?? "Subscriptions",
                Provider = EmptyToNull(Get(row, headers, "Provider")),
                AccountId = account?.Id,
                Amount = Math.Abs(ParseDecimal(Get(row, headers, "Amount", "Preis"))),
                Currency = EmptyToNull(Get(row, headers, "Currency")) ?? "CHF",
                BillingIntervalDays = Math.Max(1, (int)Math.Round(interval == 0 ? 30 : interval)),
                StartedOn = ParseDate(Get(row, headers, "Started on", "Startdatum")) ?? DateTime.UtcNow.Date,
                NextDueOn = ParseDate(Get(row, headers, "Next due", "Nächstes Fälligkeitsdatum")) ?? DateTime.UtcNow.Date,
                AutoRenew = ParseBool(Get(row, headers, "Auto renew")) || isBudgetSheet,
                Status = ParseEnum(Get(row, headers, "Status"), SubscriptionStatus.Active),
                Notes = EmptyToNull(Get(row, headers, "Notes", "Notiz")),
            };
            if (subscription.Amount <= 0)
            {
                warnings.Add($"Skipped subscription without price: {name}");
                continue;
            }
            db.Subscriptions.Add(subscription);
            existing[subscription.Name.ToLowerInvariant()] = subscription;
            count++;
        }
        return count;
    }

    static async Task<int> ImportAssets(Dictionary<string, List<List<string>>> tables, AppDbContext db, List<string> warnings)
    {
        var isBudgetSheet = false;
        if (!TryGetTable(tables, "Assets", out var rows))
        {
            isBudgetSheet = TryGetTable(tables, "Inventar", out rows);
            if (!isBudgetSheet) return 0;
        }

        var (headers, data) = SplitHeader(rows);
        var existing = await db.Items.ToDictionaryAsync(i => i.Name.ToLowerInvariant());
        var count = 0;
        foreach (var row in data)
        {
            var name = Get(row, headers, "Name", "Bezeichnung");
            if (string.IsNullOrWhiteSpace(name) || existing.ContainsKey(name.ToLowerInvariant())) continue;

            var category = Get(row, headers, "Category", "Kategorie");
            var tags = EmptyToNull(Get(row, headers, "Tags")) ?? category ?? "";
            var item = new Item
            {
                Name = name,
                Description = EmptyToNull(Get(row, headers, "Description")),
                Brand = EmptyToNull(Get(row, headers, "Brand")),
                Model = EmptyToNull(Get(row, headers, "Model")),
                SerialNumber = EmptyToNull(Get(row, headers, "Serial number", "Seriennummer")),
                Value = ParseNullableDecimal(Get(row, headers, "Value", "Kosten")),
                PurchaseDate = ParseDate(Get(row, headers, "Purchase date", "Kaufdatum")),
                WarrantyUntil = ParseDate(Get(row, headers, "Warranty until")),
                Quantity = Math.Max(1, (int)Math.Round(ParseDecimal(Get(row, headers, "Quantity")) == 0 ? 1 : ParseDecimal(Get(row, headers, "Quantity")))),
                Notes = EmptyToNull(Get(row, headers, "Notes", "Notizen")),
                TagsCsv = tags,
            };
            db.Items.Add(item);
            existing[item.Name.ToLowerInvariant()] = item;
            count++;
        }
        return count;
    }

    static byte[] BuildOds(IEnumerable<SheetData> sheets)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var mime = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var writer = new StreamWriter(mime.Open())) writer.Write("application/vnd.oasis.opendocument.spreadsheet");

            WriteEntry(zip, "content.xml", BuildContentXml(sheets));
            WriteEntry(zip, "styles.xml", """<?xml version="1.0" encoding="UTF-8"?><office:document-styles xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" office:version="1.2"/>""");
            WriteEntry(zip, "meta.xml", $"""<?xml version="1.0" encoding="UTF-8"?><office:document-meta xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" office:version="1.2"/>""");
            WriteEntry(zip, "META-INF/manifest.xml", """<?xml version="1.0" encoding="UTF-8"?><manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0" manifest:version="1.2"><manifest:file-entry manifest:full-path="/" manifest:media-type="application/vnd.oasis.opendocument.spreadsheet"/><manifest:file-entry manifest:full-path="content.xml" manifest:media-type="text/xml"/><manifest:file-entry manifest:full-path="styles.xml" manifest:media-type="text/xml"/><manifest:file-entry manifest:full-path="meta.xml" manifest:media-type="text/xml"/></manifest:manifest>""");
        }
        return stream.ToArray();
    }

    static string BuildContentXml(IEnumerable<SheetData> sheets)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.Append("""<office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" office:version="1.2"><office:body><office:spreadsheet>""");
        foreach (var sheet in sheets)
        {
            sb.Append($"""<table:table table:name="{Esc(sheet.Name)}">""");
            foreach (var row in sheet.Rows)
            {
                sb.Append("<table:table-row>");
                foreach (var cell in row)
                    sb.Append($"""<table:table-cell office:value-type="string"><text:p>{Esc(FormatCell(cell))}</text:p></table:table-cell>""");
                sb.Append("</table:table-row>");
            }
            sb.Append("</table:table>");
        }
        sb.Append("</office:spreadsheet></office:body></office:document-content>");
        return sb.ToString();
    }

    static Dictionary<string, List<List<string>>> ReadOdsTables(Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, true);
        var entry = zip.GetEntry("content.xml") ?? throw new InvalidDataException("ODS content.xml not found.");
        using var reader = entry.Open();
        var doc = XDocument.Load(reader);
        return doc.Descendants(TableNs + "table")
            .ToDictionary(
                table => table.Attribute(TableNs + "name")?.Value ?? "",
                table => table.Elements(TableNs + "table-row").Select(ReadRow).Where(r => r.Any(c => c.Length > 0)).ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    static List<string> ReadRow(XElement row)
    {
        var values = new List<string>();
        foreach (var cell in row.Elements(TableNs + "table-cell"))
        {
            var repeat = Math.Min(100, int.TryParse(cell.Attribute(TableNs + "number-columns-repeated")?.Value, out var r) ? r : 1);
            var value = string.Join(" ", cell.Descendants(TextNs + "p").Select(p => p.Value)).Trim();
            if (string.IsNullOrWhiteSpace(value))
                value = cell.Attribute(OfficeNs + "value")?.Value ?? cell.Attribute(OfficeNs + "date-value")?.Value ?? "";
            for (var i = 0; i < repeat; i++) values.Add(value);
        }
        return values;
    }

    static (Dictionary<string, int> Headers, IEnumerable<List<string>> Data) SplitHeader(List<List<string>> rows)
    {
        var headerRow = rows.FirstOrDefault(r => r.Count(c => !string.IsNullOrWhiteSpace(c)) >= 2) ?? new List<string>();
        var headers = headerRow
            .Select((name, index) => new { name = Normalize(name), index })
            .Where(x => x.name.Length > 0)
            .GroupBy(x => x.name)
            .ToDictionary(g => g.Key, g => g.First().index);
        return (headers, rows.Skip(rows.IndexOf(headerRow) + 1));
    }

    static string? Get(List<string> row, Dictionary<string, int> headers, params string[] names)
    {
        foreach (var name in names.Select(Normalize))
            if (headers.TryGetValue(name, out var index) && index < row.Count)
                return row[index].Trim();
        return null;
    }

    static bool TryGetTable(Dictionary<string, List<List<string>>> tables, string name, out List<List<string>> rows) =>
        tables.TryGetValue(name, out rows!);

    static async Task RecalculateFinanceBalances(AppDbContext db)
    {
        var accounts = await db.FinanceAccounts.ToListAsync();
        var balances = accounts.ToDictionary(a => a.Id, a => a.StartingBalance);
        var tx = await db.FinanceTransactions.ToListAsync();
        foreach (var t in tx)
        {
            if (!balances.ContainsKey(t.AccountId)) continue;
            balances[t.AccountId] += t.Kind switch
            {
                FinanceTransactionKind.Income => t.Amount,
                FinanceTransactionKind.Expense => -t.Amount,
                FinanceTransactionKind.Transfer => -t.Amount,
                _ => 0m
            };
            if (t.Kind == FinanceTransactionKind.Transfer && t.TransferAccountId.HasValue && balances.ContainsKey(t.TransferAccountId.Value))
                balances[t.TransferAccountId.Value] += t.Amount;
        }
        foreach (var account in accounts) account.Balance = balances[account.Id];
        await db.SaveChangesAsync();
    }

    static SheetData Sheet(string name, IEnumerable<object?[]> rows) =>
        new(name, rows.ToArray());

    static object?[] Row(params object?[] values) => values;

    static void WriteEntry(ZipArchive zip, string path, string contents)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(contents);
    }

    static string FormatCell(object? value) => value switch
    {
        null => "",
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        double number => number.ToString(CultureInfo.InvariantCulture),
        float number => number.ToString(CultureInfo.InvariantCulture),
        bool b => b ? "TRUE" : "FALSE",
        _ => value.ToString() ?? ""
    };

    static string Esc(string? value) => WebUtility.HtmlEncode(value ?? "");
    static string Normalize(string? value) => new((value ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    static string? FirstTag(string tagsCsv) => tagsCsv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

    static T ParseEnum<T>(string? value, T fallback) where T : struct =>
        Enum.TryParse<T>(value, true, out var parsed) ? parsed : fallback;

    static bool ParseBool(string? value) =>
        value?.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) == true ||
        value?.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase) == true ||
        value?.Trim().Equals("ja", StringComparison.OrdinalIgnoreCase) == true ||
        value?.Trim() == "1";

    static decimal? ParseNullableDecimal(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : ParseDecimal(value);

    static decimal ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        var cleaned = value
            .Replace("CHF", "", StringComparison.OrdinalIgnoreCase)
            .Replace("€", "")
            .Replace("$", "")
            .Replace("'", "")
            .Trim();
        if (decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out var invariant))
            return invariant;
        if (decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.GetCultureInfo("de-CH"), out var swiss))
            return swiss;
        return 0;
    }

    static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var formats = new[] { "yyyy-MM-dd", "dd.MM.yyyy", "dd.MM.yy", "MM/dd/yyyy" };
        if (DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var exact))
            return exact.Date;
        return DateTime.TryParse(value, CultureInfo.GetCultureInfo("de-CH"), DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed.Date
            : null;
    }

    record SheetData(string Name, object?[][] Rows);
}
