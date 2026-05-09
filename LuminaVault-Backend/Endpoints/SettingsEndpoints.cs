using LuminaVault.Data;
using LuminaVault.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

public record AssetCategoryDto(int Id, string Name, string Color, int SortOrder, DateTime CreatedAt);
public record AssetCategoryInput(string Name, string Color, int SortOrder);

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettings(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/settings").RequireAuthorization().WithTags("Settings");

        g.MapGet("/asset-categories", async (AppDbContext db) =>
            await db.AssetCategories
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Select(c => new AssetCategoryDto(c.Id, c.Name, c.Color, c.SortOrder, c.CreatedAt))
                .ToListAsync());

        g.MapPost("/asset-categories", async ([FromBody] AssetCategoryInput input, AppDbContext db) =>
        {
            var name = input.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return Results.BadRequest(new { error = "Category name is required." });
            if (await db.AssetCategories.AnyAsync(c => c.Name.ToLower() == name.ToLower()))
                return Results.Conflict(new { error = "Category already exists." });

            var category = new AssetCategory
            {
                Name = name,
                Color = string.IsNullOrWhiteSpace(input.Color) ? "#7c3aed" : input.Color.Trim(),
                SortOrder = input.SortOrder,
            };
            db.AssetCategories.Add(category);
            await db.SaveChangesAsync();
            return Results.Created($"/api/settings/asset-categories/{category.Id}",
                new AssetCategoryDto(category.Id, category.Name, category.Color, category.SortOrder, category.CreatedAt));
        });

        g.MapPut("/asset-categories/{id:int}", async (int id, [FromBody] AssetCategoryInput input, AppDbContext db) =>
        {
            var category = await db.AssetCategories.FindAsync(id);
            if (category is null) return Results.NotFound();
            var name = input.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return Results.BadRequest(new { error = "Category name is required." });
            if (await db.AssetCategories.AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower()))
                return Results.Conflict(new { error = "Category already exists." });

            category.Name = name;
            category.Color = string.IsNullOrWhiteSpace(input.Color) ? "#7c3aed" : input.Color.Trim();
            category.SortOrder = input.SortOrder;
            await db.SaveChangesAsync();
            return Results.Ok(new AssetCategoryDto(category.Id, category.Name, category.Color, category.SortOrder, category.CreatedAt));
        });

        g.MapDelete("/asset-categories/{id:int}", async (int id, AppDbContext db) =>
        {
            var category = await db.AssetCategories.FindAsync(id);
            if (category is null) return Results.NotFound();
            db.AssetCategories.Remove(category);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}
