using LuminaVault.Data;
using LuminaVault.Domain;
using LuminaVault.Storage;
using LuminaVault.Validation;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

public record DocumentAttachmentDto(
    int Id, string OriginalFileName, string Url, string ContentType, long Size, DateTime UploadedAt);

public static class AttachmentEndpoints
{
    static readonly UploadSaveOptions AttachmentUploadOptions = new()
    {
        MaxBytes = 25 * 1024 * 1024,
        MaxSizeMessage = "Max 25MB.",
        AllowedExtensions = new[]
        {
            ".pdf", ".txt", ".csv", ".ods", ".xlsx", ".doc", ".docx",
            ".jpg", ".jpeg", ".png", ".webp"
        },
        UnsupportedTypeMessage = "Unsupported file type."
    };

    public static IEndpointRouteBuilder MapAttachments(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/attachments/{id:int}", async (int id, AppDbContext db, UploadStorage uploads, CancellationToken ct) =>
        {
            var attachment = await db.DocumentAttachments.FindAsync(id);
            if (attachment is null) return Results.NotFound();
            var bytes = await uploads.ReadAsync(attachment.FileName, ct);
            return bytes is null
                ? Results.NotFound()
                : Results.File(bytes, attachment.ContentType, attachment.OriginalFileName);
        }).RequireAuthorization().WithTags("Attachments");

        var items = app.MapGroup("/api/items").RequireAuthorization().WithTags("Attachments");
        items.MapPost("/{itemId:int}/attachments", async (int itemId, IFormFile file, AppDbContext db, UploadStorage uploads, CancellationToken ct) =>
        {
            if (!await db.Items.AnyAsync(i => i.Id == itemId)) return Results.NotFound();
            var result = await SaveAttachment(file, uploads, ct);
            if (result.Error is not null) return Problem.BadRequest(result.Error);
            var attachment = result.Attachment!;
            attachment.ItemId = itemId;
            db.DocumentAttachments.Add(attachment);
            await db.SaveChangesAsync();
            return Results.Ok(MapAttachment(attachment));
        }).DisableAntiforgery().WithRequestTimeout("upload");

        var subscriptions = app.MapGroup("/api/finance/subscriptions").RequireAuthorization().WithTags("Attachments");
        subscriptions.MapPost("/{subscriptionId:int}/attachments", async (int subscriptionId, IFormFile file, AppDbContext db, UploadStorage uploads, CancellationToken ct) =>
        {
            if (!await db.Subscriptions.AnyAsync(s => s.Id == subscriptionId)) return Results.NotFound();
            var result = await SaveAttachment(file, uploads, ct);
            if (result.Error is not null) return Problem.BadRequest(result.Error);
            var attachment = result.Attachment!;
            attachment.SubscriptionId = subscriptionId;
            db.DocumentAttachments.Add(attachment);
            await db.SaveChangesAsync();
            return Results.Ok(MapAttachment(attachment));
        }).DisableAntiforgery().WithRequestTimeout("upload");

        app.MapDelete("/api/attachments/{id:int}", async (int id, AppDbContext db, UploadStorage uploads) =>
        {
            var attachment = await db.DocumentAttachments.FindAsync(id);
            if (attachment is null) return Results.NotFound();
            uploads.DeleteIfExists(attachment.FileName);
            db.DocumentAttachments.Remove(attachment);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization().WithTags("Attachments");

        return app;
    }

    public static DocumentAttachmentDto MapAttachment(DocumentAttachment attachment) =>
        new(attachment.Id, attachment.OriginalFileName, $"/api/attachments/{attachment.Id}",
            attachment.ContentType, attachment.Size, attachment.UploadedAt);

    static async Task<(DocumentAttachment? Attachment, string? Error)> SaveAttachment(
        IFormFile file,
        UploadStorage uploads,
        CancellationToken ct)
    {
        var saved = await uploads.SaveAsync(file, AttachmentUploadOptions, ct);
        if (saved.Error is not null) return (null, saved.Error);

        var upload = saved.Upload!;
        return (new DocumentAttachment
        {
            OriginalFileName = upload.OriginalFileName,
            FileName = upload.FileName,
            ContentType = upload.ContentType,
            Size = upload.Size,
        }, null);
    }
}
