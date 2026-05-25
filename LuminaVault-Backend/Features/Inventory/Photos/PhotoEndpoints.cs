using LuminaVault.Data;
using LuminaVault.Domain;
using LuminaVault.Storage;
using LuminaVault.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

public static class PhotoEndpoints
{
    static readonly UploadSaveOptions PhotoUploadOptions = new()
    {
        MaxBytes = 10 * 1024 * 1024,
        MaxSizeMessage = "Max 10MB.",
        AllowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" },
        AllowedContentTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" },
        UnsupportedTypeMessage = "Unsupported type."
    };

    public static IEndpointRouteBuilder MapPhotos(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/photos").RequireAuthorization().WithTags("Photos");

        // Authenticated media download. The frontend must fetch photos with the JWT
        // header and render them from object URLs.
        g.MapGet("/{id:int}", async (int id, AppDbContext db, UploadStorage uploads, CancellationToken ct) =>
        {
            var p = await db.ItemPhotos.FindAsync(id);
            if (p is null) return Results.NotFound();
            var bytes = await uploads.ReadAsync(p.FileName, ct);
            if (bytes is null) return Results.NotFound();
            return Results.File(bytes, p.ContentType);
        });

        var auth = app.MapGroup("/api/items").RequireAuthorization().WithTags("Photos");

        auth.MapPost("/{itemId:int}/photos", async (int itemId, IFormFile file,
            AppDbContext db, UploadStorage uploads, CancellationToken ct) =>
        {
            var item = await db.Items.FindAsync(itemId);
            if (item is null) return Results.NotFound();

            var saved = await uploads.SaveAsync(file, PhotoUploadOptions, ct);
            if (saved.Error is not null) return Problem.BadRequest(saved.Error);

            var upload = saved.Upload!;
            var photo = new ItemPhoto { ItemId = itemId, FileName = upload.FileName, ContentType = upload.ContentType };
            db.ItemPhotos.Add(photo);
            await db.SaveChangesAsync();
            return Results.Ok(new ItemPhotoDto(photo.Id, $"/api/photos/{photo.Id}", photo.ContentType));
        }).DisableAntiforgery().WithRequestTimeout("upload");

        g.MapDelete("/{id:int}", async (int id, AppDbContext db, UploadStorage uploads) =>
        {
            var p = await db.ItemPhotos.FindAsync(id);
            if (p is null) return Results.NotFound();
            uploads.DeleteIfExists(p.FileName);
            db.ItemPhotos.Remove(p);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}
