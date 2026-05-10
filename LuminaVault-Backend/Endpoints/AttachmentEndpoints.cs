using LuminaVault.Data;
using LuminaVault.Domain;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

public record DocumentAttachmentDto(
    int Id, string OriginalFileName, string Url, string ContentType, long Size, DateTime UploadedAt);

public static class AttachmentEndpoints
{
    static readonly string[] AllowedExtensions =
    {
        ".pdf", ".txt", ".csv", ".ods", ".xlsx", ".doc", ".docx",
        ".jpg", ".jpeg", ".png", ".webp"
    };

    public static IEndpointRouteBuilder MapAttachments(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/attachments/{id:int}", async (int id, AppDbContext db, IWebHostEnvironment env) =>
        {
            var attachment = await db.DocumentAttachments.FindAsync(id);
            if (attachment is null) return Results.NotFound();
            var path = UploadPath(env, attachment.FileName);
            if (!File.Exists(path)) return Results.NotFound();
            return Results.File(await File.ReadAllBytesAsync(path), attachment.ContentType, attachment.OriginalFileName);
        }).WithTags("Attachments");

        var items = app.MapGroup("/api/items").RequireAuthorization().WithTags("Attachments");
        items.MapPost("/{itemId:int}/attachments", async (int itemId, IFormFile file, AppDbContext db, IWebHostEnvironment env) =>
        {
            if (!await db.Items.AnyAsync(i => i.Id == itemId)) return Results.NotFound();
            var result = await SaveAttachment(file, env);
            if (result.Error is not null) return Results.BadRequest(new { error = result.Error });
            var attachment = result.Attachment!;
            attachment.ItemId = itemId;
            db.DocumentAttachments.Add(attachment);
            await db.SaveChangesAsync();
            return Results.Ok(MapAttachment(attachment));
        }).DisableAntiforgery();

        var subscriptions = app.MapGroup("/api/finance/subscriptions").RequireAuthorization().WithTags("Attachments");
        subscriptions.MapPost("/{subscriptionId:int}/attachments", async (int subscriptionId, IFormFile file, AppDbContext db, IWebHostEnvironment env) =>
        {
            if (!await db.Subscriptions.AnyAsync(s => s.Id == subscriptionId)) return Results.NotFound();
            var result = await SaveAttachment(file, env);
            if (result.Error is not null) return Results.BadRequest(new { error = result.Error });
            var attachment = result.Attachment!;
            attachment.SubscriptionId = subscriptionId;
            db.DocumentAttachments.Add(attachment);
            await db.SaveChangesAsync();
            return Results.Ok(MapAttachment(attachment));
        }).DisableAntiforgery();

        app.MapDelete("/api/attachments/{id:int}", async (int id, AppDbContext db, IWebHostEnvironment env) =>
        {
            var attachment = await db.DocumentAttachments.FindAsync(id);
            if (attachment is null) return Results.NotFound();
            DeleteUploadFile(env, attachment.FileName);
            db.DocumentAttachments.Remove(attachment);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization().WithTags("Attachments");

        return app;
    }

    public static DocumentAttachmentDto MapAttachment(DocumentAttachment attachment) =>
        new(attachment.Id, attachment.OriginalFileName, $"/api/attachments/{attachment.Id}",
            attachment.ContentType, attachment.Size, attachment.UploadedAt);

    static async Task<(DocumentAttachment? Attachment, string? Error)> SaveAttachment(IFormFile file, IWebHostEnvironment env)
    {
        if (file is null || file.Length == 0) return (null, "No file.");
        if (file.Length > 25 * 1024 * 1024) return (null, "Max 25MB.");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext)) return (null, "Unsupported file type.");

        var dir = Path.Combine(env.ContentRootPath, "uploads");
        Directory.CreateDirectory(dir);
        var stored = $"{Guid.NewGuid():N}{ext}";
        await using (var fs = File.Create(UploadPath(env, stored))) await file.CopyToAsync(fs);
        return (new DocumentAttachment
        {
            OriginalFileName = Path.GetFileName(file.FileName),
            FileName = stored,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            Size = file.Length,
        }, null);
    }

    public static string UploadPath(IWebHostEnvironment env, string fileName) =>
        Path.Combine(env.ContentRootPath, "uploads", fileName);

    public static void DeleteUploadFile(IWebHostEnvironment env, string fileName)
    {
        try
        {
            var path = UploadPath(env, fileName);
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }
    }
}
