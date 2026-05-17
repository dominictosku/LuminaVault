using LuminaVault.Data;
using LuminaVault.Domain;
using LuminaVault.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

public record AssetCategoryDto(int Id, string Name, string Color, int SortOrder, DateTime CreatedAt);
public record AssetCategoryInput(string Name, string Color, int SortOrder);
public record FinanceCategoryDto(int Id, string Name, string Color, int SortOrder, DateTime CreatedAt);
public record FinanceCategoryInput(string Name, string Color, int SortOrder);
public record FinanceCategoryRuleDto(
    int Id,
    string Pattern,
    string Category,
    bool MatchPayee,
    bool MatchDescription,
    bool IsActive,
    int Priority,
    DateTime CreatedAt,
    DateTime UpdatedAt);
public record FinanceCategoryRuleInput(
    string Pattern,
    string Category,
    bool MatchPayee,
    bool MatchDescription,
    bool IsActive,
    int Priority);
public record ExchangeRateDto(int Id, string Currency, DateTime EffectiveDate, decimal RateToBase, DateTime UpdatedAt);
public record ExchangeRateInput(string Currency, DateTime? EffectiveDate, decimal RateToBase);

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettings(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/settings").RequireAuthorization().WithTags("Settings");

        MapNamedCategoryCrud(g, "asset-categories",
            db => db.AssetCategories,
            c => new AssetCategoryDto(c.Id, c.Name, c.Color, c.SortOrder, c.CreatedAt));

        MapNamedCategoryCrud(g, "finance-categories",
            db => db.FinanceCategories,
            c => new FinanceCategoryDto(c.Id, c.Name, c.Color, c.SortOrder, c.CreatedAt));

        g.MapGet("/finance-category-rules", async (AppDbContext db) =>
            await db.FinanceCategoryRules
                .OrderBy(r => r.Priority)
                .ThenBy(r => r.Pattern)
                .Select(r => new FinanceCategoryRuleDto(
                    r.Id, r.Pattern, r.Category, r.MatchPayee, r.MatchDescription,
                    r.IsActive, r.Priority, r.CreatedAt, r.UpdatedAt))
                .ToListAsync());

        g.MapPost("/finance-category-rules", async ([FromBody] FinanceCategoryRuleInput input, AppDbContext db) =>
        {
            var validation = ValidateRule(input);
            if (validation is not null) return validation;

            var rule = new FinanceCategoryRule();
            ApplyRule(rule, input);
            db.FinanceCategoryRules.Add(rule);
            await EnsureFinanceCategory(db, rule.Category);
            await db.SaveChangesAsync();
            return Results.Created($"/api/settings/finance-category-rules/{rule.Id}", MapRule(rule));
        });

        g.MapPut("/finance-category-rules/{id:int}", async (int id, [FromBody] FinanceCategoryRuleInput input, AppDbContext db) =>
        {
            var rule = await db.FinanceCategoryRules.FindAsync(id);
            if (rule is null) return Results.NotFound();
            var validation = ValidateRule(input);
            if (validation is not null) return validation;

            ApplyRule(rule, input);
            rule.UpdatedAt = DateTime.UtcNow;
            await EnsureFinanceCategory(db, rule.Category);
            await db.SaveChangesAsync();
            return Results.Ok(MapRule(rule));
        });

        g.MapDelete("/finance-category-rules/{id:int}", async (int id, AppDbContext db) =>
        {
            var rule = await db.FinanceCategoryRules.FindAsync(id);
            if (rule is null) return Results.NotFound();
            db.FinanceCategoryRules.Remove(rule);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapGet("/exchange-rates", async (AppDbContext db) =>
            await db.ExchangeRates
                .OrderBy(r => r.Currency)
                .ThenByDescending(r => r.EffectiveDate)
                .Select(r => new ExchangeRateDto(r.Id, r.Currency, r.EffectiveDate, r.RateToBase, r.UpdatedAt))
                .ToListAsync());

        g.MapPost("/exchange-rates", async ([FromBody] ExchangeRateInput input, AppDbContext db) =>
        {
            var validation = ValidateRate(input);
            if (validation is not null) return validation;
            var currency = input.Currency.Trim().ToUpperInvariant();
            if (currency == CurrencyConversion.BaseCurrency)
                return Problem.BadRequest($"{CurrencyConversion.BaseCurrency} is the base currency and always has rate 1.");
            var effectiveDate = (input.EffectiveDate ?? DateTime.UtcNow.Date).Date;
            if (await db.ExchangeRates.AnyAsync(r => r.Currency == currency && r.EffectiveDate == effectiveDate))
                return Problem.Conflict("Exchange rate already exists.");

            var rate = new ExchangeRate
            {
                Currency = currency,
                EffectiveDate = effectiveDate,
                RateToBase = input.RateToBase,
                UpdatedAt = DateTime.UtcNow,
            };
            db.ExchangeRates.Add(rate);
            await db.SaveChangesAsync();
            return Results.Created($"/api/settings/exchange-rates/{rate.Id}", MapRate(rate));
        });

        g.MapPut("/exchange-rates/{id:int}", async (int id, [FromBody] ExchangeRateInput input, AppDbContext db) =>
        {
            var rate = await db.ExchangeRates.FindAsync(id);
            if (rate is null) return Results.NotFound();
            var validation = ValidateRate(input);
            if (validation is not null) return validation;
            var currency = input.Currency.Trim().ToUpperInvariant();
            if (currency == CurrencyConversion.BaseCurrency)
                return Problem.BadRequest($"{CurrencyConversion.BaseCurrency} is the base currency and always has rate 1.");
            var effectiveDate = (input.EffectiveDate ?? DateTime.UtcNow.Date).Date;
            if (await db.ExchangeRates.AnyAsync(r => r.Id != id && r.Currency == currency && r.EffectiveDate == effectiveDate))
                return Problem.Conflict("Exchange rate already exists.");

            rate.Currency = currency;
            rate.EffectiveDate = effectiveDate;
            rate.RateToBase = input.RateToBase;
            rate.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(MapRate(rate));
        });

        g.MapDelete("/exchange-rates/{id:int}", async (int id, AppDbContext db) =>
        {
            var rate = await db.ExchangeRates.FindAsync(id);
            if (rate is null) return Results.NotFound();
            db.ExchangeRates.Remove(rate);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }

    static ExchangeRateDto MapRate(ExchangeRate rate) =>
        new(rate.Id, rate.Currency, rate.EffectiveDate, rate.RateToBase, rate.UpdatedAt);

    static FinanceCategoryRuleDto MapRule(FinanceCategoryRule rule) =>
        new(rule.Id, rule.Pattern, rule.Category, rule.MatchPayee, rule.MatchDescription,
            rule.IsActive, rule.Priority, rule.CreatedAt, rule.UpdatedAt);

    static void ApplyRule(FinanceCategoryRule rule, FinanceCategoryRuleInput input)
    {
        rule.Pattern = input.Pattern.Trim();
        rule.Category = input.Category.Trim();
        rule.MatchPayee = input.MatchPayee;
        rule.MatchDescription = input.MatchDescription;
        rule.IsActive = input.IsActive;
        rule.Priority = input.Priority;
    }

    static IResult? ValidateRule(FinanceCategoryRuleInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Pattern))
            return Problem.BadRequest("Rule pattern is required.");
        if (string.IsNullOrWhiteSpace(input.Category))
            return Problem.BadRequest("Rule category is required.");
        if (!input.MatchPayee && !input.MatchDescription)
            return Problem.BadRequest("Choose at least one field to match.");
        return null;
    }

    static IResult? ValidateRate(ExchangeRateInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Currency))
            return Problem.BadRequest("Currency is required.");
        if (input.Currency.Trim().Length > 8)
            return Problem.BadRequest("Currency must be 8 characters or fewer.");
        if (input.RateToBase <= 0)
            return Problem.BadRequest("Rate must be greater than zero.");
        return null;
    }

    static async Task EnsureFinanceCategory(AppDbContext db, string category)
    {
        if (await db.FinanceCategories.AnyAsync(c => c.Name.ToLower() == category.ToLower()))
            return;
        var maxSort = await db.FinanceCategories.Select(c => (int?)c.SortOrder).MaxAsync() ?? 0;
        db.FinanceCategories.Add(new FinanceCategory
        {
            Name = category,
            Color = "#7c3aed",
            SortOrder = maxSort + 1,
        });
    }

    /// Internal payload shape for the shared category CRUD. The public records
    /// (AssetCategoryInput / FinanceCategoryInput) have the same JSON shape and bind
    /// to this one transparently; using a generic record here keeps the helper from
    /// reading as if it only handled asset categories.
    private record NamedCategoryInput(string Name, string Color, int SortOrder);

    /// Standard list/create/update/delete CRUD for `INamedCategory` entities (AssetCategory,
    /// FinanceCategory). Two settings endpoints used to copy-paste this routine; pulling it
    /// here means a behavior tweak (e.g. case-insensitive dedup, default color) ships to both.
    static void MapNamedCategoryCrud<TEntity, TDto>(
        IEndpointRouteBuilder group,
        string urlSegment,
        Func<AppDbContext, DbSet<TEntity>> dbSet,
        Func<TEntity, TDto> map)
        where TEntity : class, INamedCategory, new()
    {
        group.MapGet($"/{urlSegment}", async (AppDbContext db) =>
            await dbSet(db)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Select(c => map(c))
                .ToListAsync());

        group.MapPost($"/{urlSegment}", async ([FromBody] NamedCategoryInput input, AppDbContext db) =>
        {
            var name = input.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return Problem.BadRequest("Category name is required.");
            if (await dbSet(db).AnyAsync(c => c.Name.ToLower() == name.ToLower()))
                return Problem.Conflict("Category already exists.");

            var category = new TEntity
            {
                Name = name,
                Color = string.IsNullOrWhiteSpace(input.Color) ? "#7c3aed" : input.Color.Trim(),
                SortOrder = input.SortOrder,
            };
            dbSet(db).Add(category);
            await db.SaveChangesAsync();
            return Results.Created($"/api/settings/{urlSegment}/{category.Id}", map(category));
        });

        group.MapPut($"/{urlSegment}/{{id:int}}", async (int id, [FromBody] NamedCategoryInput input, AppDbContext db) =>
        {
            var category = await dbSet(db).FindAsync(id);
            if (category is null) return Results.NotFound();
            var name = input.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return Problem.BadRequest("Category name is required.");
            if (await dbSet(db).AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower()))
                return Problem.Conflict("Category already exists.");

            category.Name = name;
            category.Color = string.IsNullOrWhiteSpace(input.Color) ? "#7c3aed" : input.Color.Trim();
            category.SortOrder = input.SortOrder;
            await db.SaveChangesAsync();
            return Results.Ok(map(category));
        });

        group.MapDelete($"/{urlSegment}/{{id:int}}", async (int id, AppDbContext db) =>
        {
            var category = await dbSet(db).FindAsync(id);
            if (category is null) return Results.NotFound();
            dbSet(db).Remove(category);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
