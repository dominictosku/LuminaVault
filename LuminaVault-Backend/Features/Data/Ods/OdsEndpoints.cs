using LuminaVault.Data;
using LuminaVault.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static LuminaVault.Endpoints.OdsParsing;
using static LuminaVault.Endpoints.OdsTargets;

namespace LuminaVault.Endpoints;

public record OdsImportResult(
    int Accounts,
    int Transactions,
    int Holdings,
    int MonthlySummaries,
    int Subscriptions,
    int FinanceCategories,
    int AssetCategories,
    int Assets,
    string[] Warnings);

public record OdsPreviewSheet(string Name, string[] Headers, string[][] SampleRows, string SuggestedTarget);
public record OdsPreviewResult(OdsPreviewSheet[] Sheets);
public record OdsMappedImportRequest(string SheetName, string Target, Dictionary<string, string> Columns);

/// Thin orchestrator. Per-entity mappers, the .ods reader/writer, parsing primitives,
/// and balance recompute all live under Endpoints/Ods/.
public static class OdsEndpoints
{
    public static IEndpointRouteBuilder MapOdsData(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/data").RequireAuthorization().WithTags("Data");

        g.MapGet("/export/ods", async (AppDbContext db) =>
        {
            await OdsBalances.Recalculate(db);

            var accounts = await db.FinanceAccounts.OrderBy(a => a.Name).ToListAsync();
            var transactions = await db.FinanceTransactions
                .Include(t => t.Account).Include(t => t.TransferAccount)
                .OrderByDescending(t => t.OccurredOn).ThenByDescending(t => t.Id)
                .ToListAsync();
            var holdings = await db.Holdings
                .Include(h => h.Account)
                .OrderBy(h => h.Account!.Name).ThenBy(h => h.Symbol)
                .ToListAsync();
            var monthlySummaries = await db.MonthlyAccountSummaries
                .Include(s => s.Account)
                .OrderByDescending(s => s.Month).ThenBy(s => s.Account!.Name)
                .ToListAsync();
            var budgets = await db.FinanceBudgets
                .OrderByDescending(b => b.Month).ThenBy(b => b.Category)
                .ToListAsync();
            var balanceSnapshots = await db.AccountBalanceSnapshots
                .Include(s => s.Account)
                .OrderByDescending(s => s.SnapshotDate).ThenBy(s => s.Account!.Name)
                .ToListAsync();
            var subscriptions = await db.Subscriptions
                .Include(s => s.Account)
                .OrderBy(s => s.NextDueOn)
                .ToListAsync();
            var assetCategories = await db.AssetCategories
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
                .ToListAsync();
            var financeCategories = await db.FinanceCategories
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
                .ToListAsync();
            var assets = await db.Items.OrderBy(i => i.Name).ToListAsync();

            var bytes = OdsWriter.Build(new[]
            {
                OdsExport.Accounts(accounts),
                OdsExport.Transactions(transactions),
                OdsExport.Holdings(holdings),
                OdsExport.MonthlySummaries(monthlySummaries),
                OdsExport.Budgets(budgets),
                OdsExport.BalanceSnapshots(balanceSnapshots),
                OdsExport.Subscriptions(subscriptions),
                OdsExport.AssetCategories(assetCategories),
                OdsExport.FinanceCategories(financeCategories),
                OdsExport.Assets(assets),
            });

            var fileName = $"LuminaVault-export-{DateTime.UtcNow:yyyy-MM-dd}.ods";
            return Results.File(bytes, "application/vnd.oasis.opendocument.spreadsheet", fileName);
        });

        // Big sheets can take a real moment to parse + insert; bump from the 30s default.
        g.MapPost("/import/ods", async ([FromForm] IFormFile file, AppDbContext db) =>
        {
            if (file.Length == 0) return Problem.BadRequest("Choose an ODS file.");
            if (!Path.GetExtension(file.FileName).Equals(".ods", StringComparison.OrdinalIgnoreCase))
                return Problem.BadRequest("Only .ods files are supported.");

            await using var stream = file.OpenReadStream();
            var tables = OdsReader.ReadTables(stream);
            var warnings = new List<string>();

            // Order matters: accounts first (others FK to them), then categories,
            // then dependent entities. Save between accounts and the rest so newly-added
            // accounts have Ids the dependent imports can reference.
            var accountCount = await OdsImport.Accounts(tables, db, warnings);
            await db.SaveChangesAsync();
            var financeCategoryCount = await OdsImport.FinanceCategories(tables, db, warnings);
            var subscriptionCount = await OdsImport.Subscriptions(tables, db, warnings);
            var assetCategoryCount = await OdsImport.AssetCategories(tables, db, warnings);
            var assetCount = await OdsImport.Assets(tables, db, warnings);
            var monthlySummaryCount = await OdsImport.MonthlySummaries(tables, db, warnings);
            var transactionCount = await OdsImport.Transactions(tables, db, warnings);
            await db.SaveChangesAsync();
            await OdsBalances.Recalculate(db);
            await RecalculateImportedHoldings(db);
            var holdingCount = await OdsImport.Holdings(tables, db, warnings);
            await db.SaveChangesAsync();

            return Results.Ok(new OdsImportResult(
                accountCount, transactionCount, holdingCount, monthlySummaryCount, subscriptionCount,
                financeCategoryCount, assetCategoryCount, assetCount, warnings.ToArray()));
        }).DisableAntiforgery().WithRequestTimeout("upload");

        g.MapPost("/import/ods/preview", ([FromForm] IFormFile file) =>
        {
            if (file.Length == 0) return Problem.BadRequest("Choose an ODS file.");
            if (!Path.GetExtension(file.FileName).Equals(".ods", StringComparison.OrdinalIgnoreCase))
                return Problem.BadRequest("Only .ods files are supported.");
            using var stream = file.OpenReadStream();
            var tables = OdsReader.ReadTables(stream);
            var sheets = tables.Select(kv =>
            {
                var (headers, data) = SplitHeader(kv.Value);
                var orderedHeaders = headers.OrderBy(h => h.Value).Select(h =>
                    kv.Value.First(r => r.Count(c => !string.IsNullOrWhiteSpace(c)) >= 2)[h.Value]).ToArray();
                return new OdsPreviewSheet(
                    kv.Key,
                    orderedHeaders,
                    data.Take(5).Select(r => orderedHeaders.Select((_, i) => i < r.Count ? r[i] : "").ToArray()).ToArray(),
                    SuggestedTarget(kv.Key, orderedHeaders));
            }).ToArray();
            return Results.Ok(new OdsPreviewResult(sheets));
        }).DisableAntiforgery().WithRequestTimeout("upload");

        g.MapPost("/import/ods/mapped", async ([FromForm] IFormFile file, [FromForm] string mappingJson, AppDbContext db) =>
        {
            if (file.Length == 0) return Problem.BadRequest("Choose an ODS file.");
            var mapping = System.Text.Json.JsonSerializer.Deserialize<OdsMappedImportRequest>(mappingJson,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (mapping is null || string.IsNullOrWhiteSpace(mapping.SheetName) || string.IsNullOrWhiteSpace(mapping.Target))
                return Problem.BadRequest("Choose a sheet and target.");

            using var stream = file.OpenReadStream();
            var tables = OdsReader.ReadTables(stream);
            if (!tables.TryGetValue(mapping.SheetName, out var sourceRows))
                return Problem.BadRequest("Selected sheet was not found.");

            var mappedTable = OdsImport.BuildMappedTable(sourceRows, mapping.Columns);
            var targetTables = new Dictionary<string, List<List<string>>>(StringComparer.OrdinalIgnoreCase)
            {
                [CanonicalSheetName(mapping.Target)] = mappedTable
            };
            var warnings = new List<string>();
            var counts = new ImportCounts();

            switch (Normalize(mapping.Target))
            {
                case "accounts":
                    counts.Accounts = await OdsImport.Accounts(targetTables, db, warnings);
                    break;
                case "transactions":
                    counts.Transactions = await OdsImport.Transactions(targetTables, db, warnings);
                    break;
                case "holdings":
                    counts.Holdings = await OdsImport.Holdings(targetTables, db, warnings);
                    break;
                case "monthlysummaries":
                    counts.MonthlySummaries = await OdsImport.MonthlySummaries(targetTables, db, warnings);
                    break;
                case "subscriptions":
                    counts.Subscriptions = await OdsImport.Subscriptions(targetTables, db, warnings);
                    break;
                case "financecategories":
                    counts.FinanceCategories = await OdsImport.FinanceCategories(targetTables, db, warnings);
                    break;
                case "assetcategories":
                    counts.AssetCategories = await OdsImport.AssetCategories(targetTables, db, warnings);
                    break;
                case "assets":
                    counts.Assets = await OdsImport.Assets(targetTables, db, warnings);
                    break;
                default:
                    return Problem.BadRequest("Unsupported import target.");
            }

            await db.SaveChangesAsync();
            await OdsBalances.Recalculate(db);
            if (counts.Transactions > 0)
                await RecalculateImportedHoldings(db);
            return Results.Ok(new OdsImportResult(
                counts.Accounts, counts.Transactions, counts.Holdings, counts.MonthlySummaries, counts.Subscriptions,
                counts.FinanceCategories, counts.AssetCategories, counts.Assets, warnings.ToArray()));
        }).DisableAntiforgery().WithRequestTimeout("upload");

        return app;
    }

    /// Tiny mutable struct to keep the mapped-import switch readable —
    /// each branch only writes one field, leaving the others zeroed.
    struct ImportCounts
    {
        public int Accounts, Transactions, Holdings, MonthlySummaries, Subscriptions,
            FinanceCategories, AssetCategories, Assets;
    }

    static async Task RecalculateImportedHoldings(AppDbContext db)
    {
        var accountIds = await db.FinanceTransactions
            .Where(t => t.Kind == LuminaVault.Domain.FinanceTransactionKind.Buy ||
                        t.Kind == LuminaVault.Domain.FinanceTransactionKind.Sell)
            .Select(t => t.AccountId)
            .Distinct()
            .ToListAsync();
        foreach (var accountId in accountIds)
            await FinanceHelpers.RecalculateHoldings(db, accountId);
    }
}
