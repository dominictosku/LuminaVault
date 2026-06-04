using LuminaVault.Data;
using LuminaVault.Domain;
using LuminaVault.Storage;
using LuminaVault.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

public record ItemPhotoDto(int Id, string Url, string ContentType);

public record ItemDto(
    int Id, string Name, string? Category, string? Description, string? Brand, string? Model,
    string? SerialNumber, decimal? Value, DateTime? PurchaseDate, DateTime? WarrantyUntil,
    int Quantity, string? Notes, string[] Tags,
    int? FurnitureId, string? FurnitureName,
    int? ContainerId, string? ContainerName,
    int? RoomId, string? RoomName,
    DateTime CreatedAt, DateTime UpdatedAt,
    ItemPhotoDto[] Photos,
    DocumentAttachmentDto[] Attachments,
    string? ModelUrl);

public record ItemInput(
    string Name, string? Category, string? Description, string? Brand, string? Model,
    string? SerialNumber, decimal? Value, DateTime? PurchaseDate, DateTime? WarrantyUntil,
    int Quantity, string? Notes, string[] Tags,
    int? RoomId, int? FurnitureId, int? ContainerId);

public static class ItemEndpoints
{
    static readonly UploadSaveOptions ModelUploadOptions = new()
    {
        MaxBytes = 50 * 1024 * 1024,
        MaxSizeMessage = "Max 50MB.",
        AllowedExtensions = new[] { ".glb", ".gltf" },
        UnsupportedTypeMessage = "Only .glb or .gltf files are allowed.",
        ContentTypeForExtension = (extension, _) =>
            extension == ".glb" ? "model/gltf-binary" : "model/gltf+json"
    };

    public static IEndpointRouteBuilder MapItems(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/items").RequireAuthorization().WithTags("Items");

        g.MapGet("/", async (
            AppDbContext db, string? q, int? furnitureId, int? containerId, int? roomId,
            string? cursor, int? pageSize) =>
        {
            // Read-only list: opt out of change tracking so we don't pay the per-row tax
            // on what can be the largest result set in the app.
            var query = db.Items
                .AsNoTracking()
                .Include(i => i.Photos)
                .Include(i => i.Room)
                .Include(i => i.Furniture).ThenInclude(f => f!.Room)
                .Include(i => i.Container)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var s = q.Trim().ToLower();
                query = query.Where(i =>
                    i.Name.ToLower().Contains(s) ||
                    (i.Category ?? "").ToLower().Contains(s) ||
                    (i.Brand ?? "").ToLower().Contains(s) ||
                    (i.Model ?? "").ToLower().Contains(s) ||
                    (i.SerialNumber ?? "").ToLower().Contains(s) ||
                    i.TagsCsv.ToLower().Contains(s));
            }
            if (furnitureId.HasValue) query = query.Where(i => i.FurnitureId == furnitureId);
            if (containerId.HasValue) query = query.Where(i => i.ContainerId == containerId);
            if (roomId.HasValue) query = query.Where(i =>
                i.RoomId == roomId ||
                (i.Furniture != null && i.Furniture.RoomId == roomId));

            // Keyset pagination on (UpdatedAt DESC, Id DESC) — same shape as the
            // transactions list. Replaces a silent Take(500) that capped large
            // inventories without telling anyone.
            if (ItemCursor.TryDecode(cursor, out var cursorUpdated, out var cursorId))
            {
                query = query.Where(i =>
                    i.UpdatedAt < cursorUpdated ||
                    (i.UpdatedAt == cursorUpdated && i.Id < cursorId));
            }

            var size = Math.Clamp(pageSize ?? 100, 1, 500);
            var page = await query
                .OrderByDescending(i => i.UpdatedAt)
                .ThenByDescending(i => i.Id)
                // +1 to detect whether more rows exist beyond this page without a separate count.
                .Take(size + 1)
                .ToListAsync();

            string? nextCursor = null;
            if (page.Count > size)
            {
                page.RemoveAt(page.Count - 1);
                var last = page[^1];
                nextCursor = ItemCursor.Encode(last.UpdatedAt, last.Id);
            }
            return Results.Ok(new
            {
                items = page.Select(MapToDto),
                nextCursor,
            });
        });

        g.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var i = await db.Items
                .Include(x => x.Photos)
                .Include(x => x.Attachments)
                .Include(x => x.Room)
                .Include(x => x.Furniture).ThenInclude(f => f!.Room)
                .Include(x => x.Container)
                .FirstOrDefaultAsync(x => x.Id == id);
            return i is null ? Results.NotFound() : Results.Ok(MapToDto(i));
        });

        g.MapPost("/", async ([FromBody] ItemInput input, AppDbContext db) =>
        {
            var item = new Item();
            ApplyInput(item, input);
            db.Items.Add(item);
            await db.SaveChangesAsync();
            await db.Entry(item).Reference(x => x.Room).LoadAsync();
            await db.Entry(item).Reference(x => x.Furniture).LoadAsync();
            if (item.Furniture != null)
                await db.Entry(item.Furniture).Reference(f => f.Room).LoadAsync();
            await db.Entry(item).Reference(x => x.Container).LoadAsync();
            return Results.Created($"/api/items/{item.Id}", MapToDto(item));
        });

        g.MapPut("/{id:int}", async (int id, [FromBody] ItemInput input, AppDbContext db) =>
        {
            var item = await db.Items
                .Include(x => x.Photos)
                .Include(x => x.Attachments)
                .Include(x => x.Room)
                .Include(x => x.Furniture).ThenInclude(f => f!.Room)
                .Include(x => x.Container)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (item is null) return Results.NotFound();
            ApplyInput(item, input);
            item.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await db.Entry(item).Reference(x => x.Room).LoadAsync();
            return Results.Ok(MapToDto(item));
        });

        g.MapDelete("/{id:int}", async (int id, AppDbContext db, UploadStorage uploads) =>
        {
            var item = await db.Items
                .Include(x => x.Photos)
                .Include(x => x.Attachments)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (item is null) return Results.NotFound();
            foreach (var p in item.Photos) uploads.DeleteIfExists(p.FileName);
            foreach (var attachment in item.Attachments) uploads.DeleteIfExists(attachment.FileName);
            uploads.DeleteIfExists(item.ModelFileName);
            db.Items.Remove(item);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Model upload (single .glb/.gltf per item)
        g.MapPost("/{id:int}/model", async (int id, IFormFile file,
            AppDbContext db, UploadStorage uploads, CancellationToken ct) =>
        {
            var item = await db.Items.FindAsync(id);
            if (item is null) return Results.NotFound();

            var saved = await uploads.SaveAsync(file, ModelUploadOptions, ct);
            if (saved.Error is not null) return Problem.BadRequest(saved.Error);

            // Replace previous model file if any
            uploads.DeleteIfExists(item.ModelFileName);

            var upload = saved.Upload!;
            item.ModelFileName = upload.FileName;
            item.ModelContentType = upload.ContentType;
            item.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { modelUrl = $"/api/items/{item.Id}/model" });
        }).DisableAntiforgery().WithRequestTimeout("upload");

        g.MapDelete("/{id:int}/model", async (int id, AppDbContext db, UploadStorage uploads) =>
        {
            var item = await db.Items.FindAsync(id);
            if (item is null || item.ModelFileName is null) return Results.NotFound();
            uploads.DeleteIfExists(item.ModelFileName);
            item.ModelFileName = null;
            item.ModelContentType = null;
            item.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Authenticated model download. Three.js callers need to attach the JWT
        // header when loading this URL.
        app.MapGet("/api/items/{id:int}/model", async (int id, AppDbContext db, UploadStorage uploads, CancellationToken ct) =>
        {
            var item = await db.Items.FindAsync(id);
            if (item?.ModelFileName is null) return Results.NotFound();
            var bytes = await uploads.ReadAsync(item.ModelFileName, ct);
            if (bytes is null) return Results.NotFound();
            return Results.File(bytes, item.ModelContentType ?? "application/octet-stream");
        }).RequireAuthorization().WithTags("Items");

        // Stats
        g.MapGet("/stats/summary", async (AppDbContext db) =>
        {
            var totalItems = await db.Items.SumAsync(i => (int?)i.Quantity) ?? 0;
            var totalValue = await db.Items.SumAsync(i => (decimal?)(i.Value * i.Quantity)) ?? 0m;

            var rooms = await db.Rooms.ToDictionaryAsync(r => r.Id, r => r.Name);
            var perRoom = new Dictionary<int, int>();
            await foreach (var i in db.Items.Include(x => x.Furniture).AsAsyncEnumerable())
            {
                var rid = i.RoomId ?? i.Furniture?.RoomId;
                if (rid is null) continue;
                perRoom[rid.Value] = perRoom.GetValueOrDefault(rid.Value) + i.Quantity;
            }
            var byRoom = perRoom
                .Where(kv => rooms.ContainsKey(kv.Key))
                .Select(kv => new { roomId = kv.Key, roomName = rooms[kv.Key], count = kv.Value })
                .OrderByDescending(x => x.count)
                .ToList();

            var recent = await db.Items.OrderByDescending(i => i.CreatedAt).Take(5)
                .Select(i => new { i.Id, i.Name, i.CreatedAt }).ToListAsync();
            return Results.Ok(new { totalItems, totalValue, byRoom, recent });
        });

        return app;
    }

    static void ApplyInput(Item item, ItemInput input)
    {
        item.Name = input.Name;
        item.Category = string.IsNullOrWhiteSpace(input.Category) ? null : input.Category.Trim();
        item.Description = input.Description;
        item.Brand = input.Brand;
        item.Model = input.Model;
        item.SerialNumber = input.SerialNumber;
        item.Value = input.Value;
        item.PurchaseDate = input.PurchaseDate;
        item.WarrantyUntil = input.WarrantyUntil;
        item.Quantity = Math.Max(1, input.Quantity);
        item.Notes = input.Notes;
        item.TagsCsv = string.Join(",", (input.Tags ?? Array.Empty<string>())
            .Select(t => t.Trim()).Where(t => t.Length > 0));
        item.RoomId = input.RoomId;
        item.FurnitureId = input.FurnitureId;
        item.ContainerId = input.ContainerId;
    }

    public static ItemDto MapToDto(Item i)
    {
        var roomId = i.RoomId ?? i.Furniture?.RoomId;
        var roomName = i.Room?.Name ?? i.Furniture?.Room?.Name;
        return new ItemDto(
            i.Id, i.Name, i.Category, i.Description, i.Brand, i.Model,
            i.SerialNumber, i.Value, i.PurchaseDate, i.WarrantyUntil,
            i.Quantity, i.Notes,
            string.IsNullOrWhiteSpace(i.TagsCsv) ? Array.Empty<string>() : i.TagsCsv.Split(','),
            i.FurnitureId, i.Furniture?.Name,
            i.ContainerId, i.Container?.Name,
            roomId, roomName,
            i.CreatedAt, i.UpdatedAt,
            i.Photos.Select(p => new ItemPhotoDto(p.Id, $"/api/photos/{p.Id}", p.ContentType)).ToArray(),
            i.Attachments.Select(AttachmentEndpoints.MapAttachment).ToArray(),
            i.ModelFileName == null ? null : $"/api/items/{i.Id}/model");
    }

}

/// Opaque base64 cursor that pins keyset pagination to (UpdatedAt, Id). The timestamp
/// is encoded as ticks for full precision — unlike the date-only transaction cursor,
/// item UpdatedAt carries a time component. Bad cursors decode to no-op rather than
/// 400, so a stale or fabricated cursor just yields the first page.
internal static class ItemCursor
{
    public static string Encode(DateTime updatedAt, int id) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            $"{updatedAt.Ticks}|{id}"));

    public static bool TryDecode(string? cursor, out DateTime updatedAt, out int id)
    {
        updatedAt = default;
        id = 0;
        if (string.IsNullOrWhiteSpace(cursor)) return false;
        try
        {
            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = decoded.Split('|', 2);
            if (parts.Length != 2) return false;
            if (!long.TryParse(parts[0], out var ticks)) return false;
            if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks) return false;
            updatedAt = new DateTime(ticks);
            return int.TryParse(parts[1], out id);
        }
        catch
        {
            return false;
        }
    }
}
