using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LuminaVault.Data;
using LuminaVault.Domain;
using LuminaVault.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static LuminaVault.Endpoints.FinanceHelpers;

namespace LuminaVault.Endpoints;

public record BankCsvPreviewResult(
    string[] Headers,
    string[][] SampleRows,
    Dictionary<string, string> SuggestedColumns);

public record BankCsvImportRequest(
    int AccountId,
    Dictionary<string, string> Columns,
    string? DefaultCategory,
    FinanceTransactionStatus Status);

public record BankCsvImportResult(
    int Transactions,
    int Duplicates,
    int Skipped,
    string[] Warnings);

internal static class BankCsvEndpoints
{
    private static readonly string[] CanonicalFields =
    [
        "Date",
        "Payee",
        "Amount",
        "Debit",
        "Credit",
        "Category",
        "Description",
        "Notes",
        "Tags",
        "Status"
    ];

    public static IEndpointRouteBuilder MapBankCsvData(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/data/import/bank-csv").RequireAuthorization().WithTags("Data");

        g.MapPost("/preview", ([FromForm] IFormFile file) =>
        {
            if (file.Length == 0) return Problem.BadRequest("Choose a CSV file.");
            if (!Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
                return Problem.BadRequest("Only .csv files are supported.");

            using var stream = file.OpenReadStream();
            var table = BankCsvReader.Read(stream);
            if (table.Headers.Length == 0)
                return Problem.BadRequest("The CSV file does not contain a header row.");

            return Results.Ok(new BankCsvPreviewResult(
                table.Headers,
                table.Rows.Take(8).Select(r => PadRow(r, table.Headers.Length)).ToArray(),
                SuggestColumns(table.Headers)));
        }).DisableAntiforgery().WithRequestTimeout("upload");

        g.MapPost("/", async (
            [FromForm] IFormFile file,
            [FromForm] string mappingJson,
            AppDbContext db) =>
        {
            if (file.Length == 0) return Problem.BadRequest("Choose a CSV file.");
            if (!Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
                return Problem.BadRequest("Only .csv files are supported.");

            BankCsvImportRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<BankCsvImportRequest>(mappingJson,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new JsonStringEnumConverter() },
                    });
            }
            catch (JsonException)
            {
                return Problem.BadRequest("Import mapping is invalid.");
            }
            if (request is null)
                return Problem.BadRequest("Import mapping is missing.");
            if (request.Columns is null)
                return Problem.BadRequest("Import mapping columns are missing.");
            if (await Validate.FinanceAccountExists(db, request.AccountId) is { } accountFailure)
                return accountFailure;
            if (!HasMapped(request.Columns, "Date"))
                return Problem.BadRequest("Map a Date column.");
            if (!HasMapped(request.Columns, "Payee"))
                return Problem.BadRequest("Map a Payee column.");
            if (!HasMapped(request.Columns, "Amount") &&
                (!HasMapped(request.Columns, "Debit") || !HasMapped(request.Columns, "Credit")))
                return Problem.BadRequest("Map either Amount, or both Debit and Credit columns.");

            using var stream = file.OpenReadStream();
            var table = BankCsvReader.Read(stream);
            var existingKeys = await ExistingDuplicateKeys(db, request.AccountId);
            var categoryRules = await db.FinanceCategoryRules
                .Where(r => r.IsActive)
                .OrderBy(r => r.Priority)
                .ThenBy(r => r.Pattern)
                .ToListAsync();
            var warnings = new List<string>();
            var inserted = 0;
            var duplicates = 0;
            var skipped = 0;

            await using var tx = await db.Database.BeginTransactionAsync();
            foreach (var row in table.Rows)
            {
                var parsed = ParseRow(row, table.Headers, request, categoryRules, warnings);
                if (parsed is null)
                {
                    skipped++;
                    continue;
                }

                var key = DuplicateKey(
                    request.AccountId,
                    parsed.OccurredOn,
                    parsed.Kind,
                    parsed.Payee,
                    parsed.Amount);
                if (existingKeys.Contains(key))
                {
                    duplicates++;
                    continue;
                }

                db.FinanceTransactions.Add(new FinanceTransaction
                {
                    AccountId = request.AccountId,
                    Kind = parsed.Kind,
                    Status = parsed.Status,
                    OccurredOn = parsed.OccurredOn,
                    Payee = parsed.Payee,
                    Category = parsed.Category,
                    Amount = parsed.Amount,
                    Description = parsed.Description,
                    Notes = parsed.Notes,
                    TagsCsv = parsed.TagsCsv,
                });
                existingKeys.Add(key);
                inserted++;
            }

            await db.SaveChangesAsync();
            await RecalculateBalances(db);
            await tx.CommitAsync();

            return Results.Ok(new BankCsvImportResult(inserted, duplicates, skipped, warnings.ToArray()));
        }).DisableAntiforgery().WithRequestTimeout("upload");

        return app;
    }

    private static ParsedBankTransaction? ParseRow(
        string[] row,
        string[] headers,
        BankCsvImportRequest request,
        IReadOnlyCollection<FinanceCategoryRule> categoryRules,
        List<string> warnings)
    {
        var dateText = Get(row, headers, request.Columns, "Date");
        var payee = Clean(Get(row, headers, request.Columns, "Payee"));
        if (string.IsNullOrWhiteSpace(payee))
            payee = Clean(Get(row, headers, request.Columns, "Description")) ?? "Imported transaction";

        if (!TryParseDate(dateText, out var date))
        {
            warnings.Add($"Skipped row with invalid date: {dateText}");
            return null;
        }

        var amountText = Get(row, headers, request.Columns, "Amount");
        var debitText = Get(row, headers, request.Columns, "Debit");
        var creditText = Get(row, headers, request.Columns, "Credit");

        var amount = 0m;
        var kind = FinanceTransactionKind.Expense;
        if (!string.IsNullOrWhiteSpace(amountText))
        {
            if (!TryParseMoney(amountText, out var signedAmount) || signedAmount == 0)
            {
                warnings.Add($"Skipped row with invalid amount: {amountText}");
                return null;
            }

            kind = signedAmount < 0 ? FinanceTransactionKind.Expense : FinanceTransactionKind.Income;
            amount = Math.Abs(signedAmount);
        }
        else
        {
            TryParseMoney(debitText, out var debit);
            TryParseMoney(creditText, out var credit);
            if (debit == 0 && credit == 0)
            {
                warnings.Add($"Skipped row with empty debit/credit amount: {payee}");
                return null;
            }

            kind = credit > 0 ? FinanceTransactionKind.Income : FinanceTransactionKind.Expense;
            amount = credit > 0 ? credit : Math.Abs(debit);
        }

        var statusText = Get(row, headers, request.Columns, "Status");
        var status = Enum.TryParse<FinanceTransactionStatus>(statusText, true, out var parsedStatus)
            ? parsedStatus
            : request.Status;

        var mappedCategory = Clean(Get(row, headers, request.Columns, "Category"));
        var category = FinanceCategoryRules.ResolveCategory(
                categoryRules,
                mappedCategory ?? Clean(request.DefaultCategory),
                payee,
                Get(row, headers, request.Columns, "Description"),
                Get(row, headers, request.Columns, "Notes"))
            ?? mappedCategory
            ?? Clean(request.DefaultCategory)
            ?? "Imported";

        return new ParsedBankTransaction(
            date.Date,
            payee!,
            kind,
            status,
            amount,
            category,
            Clean(Get(row, headers, request.Columns, "Description")),
            Clean(Get(row, headers, request.Columns, "Notes")),
            Clean(Get(row, headers, request.Columns, "Tags")) ?? "");
    }

    private static async Task<HashSet<string>> ExistingDuplicateKeys(AppDbContext db, int accountId)
    {
        var existing = await db.FinanceTransactions
            .Where(t => t.AccountId == accountId)
            .Where(t => t.Kind == FinanceTransactionKind.Income || t.Kind == FinanceTransactionKind.Expense)
            .Select(t => new { t.OccurredOn, t.Kind, t.Payee, t.Amount })
            .ToListAsync();
        return existing
            .Select(t => DuplicateKey(accountId, t.OccurredOn, t.Kind, t.Payee, t.Amount))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string DuplicateKey(
        int accountId,
        DateTime date,
        FinanceTransactionKind kind,
        string payee,
        decimal amount) =>
        string.Join("|",
            accountId,
            date.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            kind,
            Normalize(payee),
            Math.Round(amount, 2).ToString("0.00", CultureInfo.InvariantCulture));

    private static Dictionary<string, string> SuggestColumns(string[] headers)
    {
        var result = CanonicalFields.ToDictionary(f => f, _ => "");
        foreach (var field in CanonicalFields)
        {
            result[field] = headers.FirstOrDefault(h => IsLikely(field, h)) ?? "";
        }
        return result;
    }

    private static bool IsLikely(string field, string header)
    {
        var h = Normalize(header);
        return field switch
        {
            "Date" => h is "date" or "bookingdate" or "valuedate" or "transactiondate" or "datum" or "buchungsdatum",
            "Payee" => h is "payee" or "merchant" or "partner" or "name" or "recipient" or "beneficiary" or "gegenpartei" or "zahlungsempfaenger",
            "Amount" => h is "amount" or "betrag" or "value",
            "Debit" => h is "debit" or "withdrawal" or "charge" or "belastung" or "soll",
            "Credit" => h is "credit" or "deposit" or "gutschrift" or "haben",
            "Category" => h is "category" or "kategorie",
            "Description" => h is "description" or "memo" or "text" or "details" or "verwendungszweck" or "beschreibung",
            "Notes" => h is "notes" or "note" or "notizen" or "notiz",
            "Tags" => h is "tags" or "labels",
            "Status" => h is "status",
            _ => false,
        };
    }

    private static string? Get(
        string[] row,
        string[] headers,
        Dictionary<string, string> columns,
        string canonicalField)
    {
        if (!columns.TryGetValue(canonicalField, out var source) || string.IsNullOrWhiteSpace(source))
            return null;
        var index = Array.FindIndex(headers, h => string.Equals(h, source, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index < row.Length ? row[index] : null;
    }

    private static bool HasMapped(Dictionary<string, string> columns, string field) =>
        columns.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value);

    private static bool TryParseDate(string? value, out DateTime date)
    {
        var formats = new[]
        {
            "yyyy-MM-dd", "yyyy/MM/dd", "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy",
            "MM/dd/yyyy", "M/d/yyyy", "dd-MM-yyyy", "d-M-yyyy", "yyyyMMdd"
        };
        return DateTime.TryParseExact(value?.Trim(), formats, CultureInfo.InvariantCulture,
                   DateTimeStyles.AssumeLocal, out date)
               || DateTime.TryParse(value, CultureInfo.GetCultureInfo("de-CH"), DateTimeStyles.AssumeLocal, out date)
               || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out date);
    }

    private static bool TryParseMoney(string? value, out decimal amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var cleaned = value.Trim()
            .Replace("CHF", "", StringComparison.OrdinalIgnoreCase)
            .Replace("EUR", "", StringComparison.OrdinalIgnoreCase)
            .Replace("USD", "", StringComparison.OrdinalIgnoreCase)
            .Replace("'", "")
            .Replace(" ", "");

        var isNegative = cleaned.StartsWith("(", StringComparison.Ordinal) && cleaned.EndsWith(")", StringComparison.Ordinal);
        cleaned = cleaned.Trim('(', ')');

        if (cleaned.Contains(',') && cleaned.Contains('.'))
        {
            var lastComma = cleaned.LastIndexOf(',');
            var lastDot = cleaned.LastIndexOf('.');
            cleaned = lastComma > lastDot
                ? cleaned.Replace(".", "").Replace(",", ".")
                : cleaned.Replace(",", "");
        }
        else if (cleaned.Contains(','))
        {
            cleaned = cleaned.Replace(",", ".");
        }

        if (!decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out amount))
            return false;
        if (isNegative) amount = -amount;
        return true;
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) builder.Append(c);
        }
        return builder.ToString();
    }

    private static string[] PadRow(string[] row, int length) =>
        row.Length >= length ? row.Take(length).ToArray() : row.Concat(Enumerable.Repeat("", length - row.Length)).ToArray();

    private record ParsedBankTransaction(
        DateTime OccurredOn,
        string Payee,
        FinanceTransactionKind Kind,
        FinanceTransactionStatus Status,
        decimal Amount,
        string Category,
        string? Description,
        string? Notes,
        string TagsCsv);
}
