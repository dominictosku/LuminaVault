using LuminaVault.Data;
using LuminaVault.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

public record FurnitureDto(int Id, int RoomId, string Name, FurnitureKind Kind,
    double X, double Y, double Z, double Width, double Depth, double Height, double RotationY,
    int ItemCount, int ContainerCount);

public record FurnitureInput(string Name, FurnitureKind Kind,
    double X, double Y, double Z, double Width, double Depth, double Height, double RotationY);

public record ContainerDto(int Id, int FurnitureId, string Name, string? Description, int ItemCount);
public record ContainerInput(string Name, string? Description);

public static class FurnitureEndpoints
{
    public static IEndpointRouteBuilder MapFurniture(this IEndpointRouteBuilder app)
    {
        var rooms = app.MapGroup("/api/rooms").RequireAuthorization().WithTags("Furniture");

        rooms.MapGet("/{roomId:int}/furniture", async (int roomId, AppDbContext db) =>
            await db.Furniture.Where(f => f.RoomId == roomId)
                .Select(f => new FurnitureDto(f.Id, f.RoomId, f.Name, f.Kind,
                    f.X, f.Y, f.Z, f.Width, f.Depth, f.Height, f.RotationY,
                    f.Items.Count, f.Containers.Count))
                .ToListAsync());

        rooms.MapPost("/{roomId:int}/furniture", async (int roomId, [FromBody] FurnitureInput input, AppDbContext db) =>
        {
            if (!await db.Rooms.AnyAsync(r => r.Id == roomId)) return Results.NotFound();
            var f = new Furniture
            {
                RoomId = roomId,
                Name = input.Name, Kind = input.Kind,
                X = input.X, Y = input.Y, Z = input.Z,
                Width = input.Width, Depth = input.Depth, Height = input.Height,
                RotationY = input.RotationY
            };
            db.Furniture.Add(f);
            await db.SaveChangesAsync();
            return Results.Created($"/api/furniture/{f.Id}",
                new FurnitureDto(f.Id, f.RoomId, f.Name, f.Kind,
                    f.X, f.Y, f.Z, f.Width, f.Depth, f.Height, f.RotationY, 0, 0));
        });

        var fg = app.MapGroup("/api/furniture").RequireAuthorization().WithTags("Furniture");

        fg.MapPut("/{id:int}", async (int id, [FromBody] FurnitureInput input, AppDbContext db) =>
        {
            var f = await db.Furniture.FindAsync(id);
            if (f is null) return Results.NotFound();
            f.Name = input.Name; f.Kind = input.Kind;
            f.X = input.X; f.Y = input.Y; f.Z = input.Z;
            f.Width = input.Width; f.Depth = input.Depth; f.Height = input.Height;
            f.RotationY = input.RotationY;
            await db.SaveChangesAsync();
            return Results.Ok(new FurnitureDto(f.Id, f.RoomId, f.Name, f.Kind,
                f.X, f.Y, f.Z, f.Width, f.Depth, f.Height, f.RotationY,
                await db.Items.CountAsync(i => i.FurnitureId == f.Id),
                await db.Containers.CountAsync(c => c.FurnitureId == f.Id)));
        });

        fg.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var f = await db.Furniture.FindAsync(id);
            if (f is null) return Results.NotFound();
            db.Furniture.Remove(f);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Containers nested
        fg.MapGet("/{furnitureId:int}/containers", async (int furnitureId, AppDbContext db) =>
            await db.Containers.Where(c => c.FurnitureId == furnitureId)
                .Select(c => new ContainerDto(c.Id, c.FurnitureId, c.Name, c.Description, c.Items.Count))
                .ToListAsync());

        fg.MapPost("/{furnitureId:int}/containers", async (int furnitureId, [FromBody] ContainerInput input, AppDbContext db) =>
        {
            if (!await db.Furniture.AnyAsync(f => f.Id == furnitureId)) return Results.NotFound();
            var c = new Container { FurnitureId = furnitureId, Name = input.Name, Description = input.Description };
            db.Containers.Add(c);
            await db.SaveChangesAsync();
            return Results.Created($"/api/containers/{c.Id}",
                new ContainerDto(c.Id, c.FurnitureId, c.Name, c.Description, 0));
        });

        var cg = app.MapGroup("/api/containers").RequireAuthorization().WithTags("Containers");

        cg.MapPut("/{id:int}", async (int id, [FromBody] ContainerInput input, AppDbContext db) =>
        {
            var c = await db.Containers.FindAsync(id);
            if (c is null) return Results.NotFound();
            c.Name = input.Name; c.Description = input.Description;
            await db.SaveChangesAsync();
            return Results.Ok(new ContainerDto(c.Id, c.FurnitureId, c.Name, c.Description,
                await db.Items.CountAsync(i => i.ContainerId == c.Id)));
        });

        cg.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var c = await db.Containers.FindAsync(id);
            if (c is null) return Results.NotFound();
            db.Containers.Remove(c);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}
