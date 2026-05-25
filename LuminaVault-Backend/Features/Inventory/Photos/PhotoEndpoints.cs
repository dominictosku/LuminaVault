using LuminaVault.Data;
using LuminaVault.Domain;
using LuminaVault.Storage;
using LuminaVault.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

public static class PhotoEndpoints
{
    static readonly string[] Allowed = { "image/jpeg", "image/png", "image/webp", "image/gif" };

    public static IEndpointRouteBuilder MapPhotos(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/photos").RequireAuthorization().WithTags("Photos");

        // Authenticated media download. The frontend must fetch photos with the JWT
        // header and render them from object URLs.
        g.MapGet("/{id:int}", async (int id, AppDbContext db, StoragePaths storage) =>
        {
            var p = await db.ItemPhotos.FindAsync(id);
            if (p is null) return Results.NotFound();
            var path = storage.UploadPath(p.FileName);
            if (!File.Exists(path)) return Results.NotFound();
            var bytes = await File.ReadAllBytesAsync(path);
            return Results.File(bytes, p.ContentType);
        });

        var auth = app.MapGroup("/api/items").RequireAuthorization().WithTags("Photos");

        auth.MapPost("/{itemId:int}/photos", async (int itemId, IFormFile file,
            AppDbContext db, StoragePaths storage) =>
        {
            if (file is null || file.Length == 0) return Problem.BadRequest("No file.");
            if (file.Length > 10 * 1024 * 1024) return Problem.BadRequest("Max 10MB.");
            if (!Allowed.Contains(file.ContentType)) return Problem.BadRequest("Unsupported type.");

            var item = await db.Items.FindAsync(itemId);
            if (item is null) return Results.NotFound();

            Directory.CreateDirectory(storage.UploadsDirectory);
            var ext = Path.GetExtension(file.FileName);
            var name = $"{Guid.NewGuid():N}{ext}";
            var path = storage.UploadPath(name);
            await using (var fs = File.Create(path)) await file.CopyToAsync(fs);

            var photo = new ItemPhoto { ItemId = itemId, FileName = name, ContentType = file.ContentType };
            db.ItemPhotos.Add(photo);
            await db.SaveChangesAsync();
            return Results.Ok(new ItemPhotoDto(photo.Id, $"/api/photos/{photo.Id}", photo.ContentType));
        }).DisableAntiforgery().WithRequestTimeout("upload");

        g.MapDelete("/{id:int}", async (int id, AppDbContext db, StoragePaths storage) =>
        {
            var p = await db.ItemPhotos.FindAsync(id);
            if (p is null) return Results.NotFound();
            try
            {
                var path = storage.UploadPath(p.FileName);
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
            db.ItemPhotos.Remove(p);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}
