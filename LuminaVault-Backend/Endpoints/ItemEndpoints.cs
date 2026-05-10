using LuminaVault.Data;
using LuminaVault.Domain;
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
    public static IEndpointRouteBuilder MapItems(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/items").RequireAuthorization().WithTags("Items");

        g.MapGet("/", async (AppDbContext db, string? q, int? furnitureId, int? containerId, int? roomId) =>
        {
            var query = db.Items
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

            var items = await query.OrderByDescending(i => i.UpdatedAt).Take(500).ToListAsync();
            return Results.Ok(items.Select(MapToDto));
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

        g.MapDelete("/{id:int}", async (int id, AppDbContext db, IWebHostEnvironment env) =>
        {
            var item = await db.Items
                .Include(x => x.Photos)
                .Include(x => x.Attachments)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (item is null) return Results.NotFound();
            foreach (var p in item.Photos) DeleteUploadFile(env, p.FileName);
            foreach (var attachment in item.Attachments) DeleteUploadFile(env, attachment.FileName);
            if (item.ModelFileName != null) DeleteUploadFile(env, item.ModelFileName);
            db.Items.Remove(item);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Model upload (single .glb/.gltf per item)
        g.MapPost("/{id:int}/model", async (int id, IFormFile file,
            AppDbContext db, IWebHostEnvironment env) =>
        {
            if (file is null || file.Length == 0) return Results.BadRequest(new { error = "No file." });
            if (file.Length > 50 * 1024 * 1024) return Results.BadRequest(new { error = "Max 50MB." });
            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (ext != ".glb" && ext != ".gltf")
                return Results.BadRequest(new { error = "Only .glb or .gltf files are allowed." });

            var item = await db.Items.FindAsync(id);
            if (item is null) return Results.NotFound();

            var dir = Path.Combine(env.ContentRootPath, "uploads");
            Directory.CreateDirectory(dir);
            var name = $"{Guid.NewGuid():N}{ext}";
            var path = Path.Combine(dir, name);
            await using (var fs = File.Create(path)) await file.CopyToAsync(fs);

            // Replace previous model file if any
            if (item.ModelFileName != null) DeleteUploadFile(env, item.ModelFileName);

            item.ModelFileName = name;
            item.ModelContentType = ext == ".glb" ? "model/gltf-binary" : "model/gltf+json";
            item.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { modelUrl = $"/api/items/{item.Id}/model" });
        }).DisableAntiforgery();

        g.MapDelete("/{id:int}/model", async (int id, AppDbContext db, IWebHostEnvironment env) =>
        {
            var item = await db.Items.FindAsync(id);
            if (item is null || item.ModelFileName is null) return Results.NotFound();
            DeleteUploadFile(env, item.ModelFileName);
            item.ModelFileName = null;
            item.ModelContentType = null;
            item.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Public model download (no auth required so <model-viewer>/loaders work without headers)
        app.MapGet("/api/items/{id:int}/model", async (int id, AppDbContext db, IWebHostEnvironment env) =>
        {
            var item = await db.Items.FindAsync(id);
            if (item?.ModelFileName is null) return Results.NotFound();
            var path = Path.Combine(env.ContentRootPath, "uploads", item.ModelFileName);
            if (!File.Exists(path)) return Results.NotFound();
            var bytes = await File.ReadAllBytesAsync(path);
            return Results.File(bytes, item.ModelContentType ?? "application/octet-stream");
        }).WithTags("Items");

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

    static void DeleteUploadFile(IWebHostEnvironment env, string fileName)
    {
        try
        {
            var path = Path.Combine(env.ContentRootPath, "uploads", fileName);
            if (File.Exists(path)) File.Delete(path);
        }
        catch { /* best effort */ }
    }
}
