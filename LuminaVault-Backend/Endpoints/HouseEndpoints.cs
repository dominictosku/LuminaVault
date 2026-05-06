using LuminaVault.Data;
using LuminaVault.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

public record HouseDto(int Id, string Name, string? Description);
public record HouseInput(string Name, string? Description);

public record RoomDto(int Id, int HouseId, string Name, string Color,
    double X, double Z, double Width, double Depth, double Height);
public record RoomInput(string Name, string Color,
    double X, double Z, double Width, double Depth, double Height);

public static class HouseEndpoints
{
    public static IEndpointRouteBuilder MapHouses(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/houses").RequireAuthorization().WithTags("Houses");

        g.MapGet("/", async (AppDbContext db) =>
            await db.Houses.Select(h => new HouseDto(h.Id, h.Name, h.Description)).ToListAsync());

        g.MapPost("/", async ([FromBody] HouseInput input, AppDbContext db) =>
        {
            var h = new House { Name = input.Name, Description = input.Description };
            db.Houses.Add(h);
            await db.SaveChangesAsync();
            return Results.Created($"/api/houses/{h.Id}", new HouseDto(h.Id, h.Name, h.Description));
        });

        g.MapPut("/{id:int}", async (int id, [FromBody] HouseInput input, AppDbContext db) =>
        {
            var h = await db.Houses.FindAsync(id);
            if (h is null) return Results.NotFound();
            h.Name = input.Name;
            h.Description = input.Description;
            await db.SaveChangesAsync();
            return Results.Ok(new HouseDto(h.Id, h.Name, h.Description));
        });

        g.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var h = await db.Houses.FindAsync(id);
            if (h is null) return Results.NotFound();
            db.Houses.Remove(h);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Rooms nested under house
        g.MapGet("/{houseId:int}/rooms", async (int houseId, AppDbContext db) =>
            await db.Rooms.Where(r => r.HouseId == houseId)
                .Select(r => new RoomDto(r.Id, r.HouseId, r.Name, r.Color, r.X, r.Z, r.Width, r.Depth, r.Height))
                .ToListAsync());

        g.MapPost("/{houseId:int}/rooms", async (int houseId, [FromBody] RoomInput input, AppDbContext db) =>
        {
            if (!await db.Houses.AnyAsync(h => h.Id == houseId)) return Results.NotFound();
            var r = new Room
            {
                HouseId = houseId,
                Name = input.Name,
                Color = input.Color,
                X = input.X, Z = input.Z,
                Width = input.Width, Depth = input.Depth, Height = input.Height
            };
            db.Rooms.Add(r);
            await db.SaveChangesAsync();
            return Results.Created($"/api/rooms/{r.Id}",
                new RoomDto(r.Id, r.HouseId, r.Name, r.Color, r.X, r.Z, r.Width, r.Depth, r.Height));
        });

        var r = app.MapGroup("/api/rooms").RequireAuthorization().WithTags("Rooms");

        r.MapPut("/{id:int}", async (int id, [FromBody] RoomInput input, AppDbContext db) =>
        {
            var room = await db.Rooms.FindAsync(id);
            if (room is null) return Results.NotFound();
            room.Name = input.Name;
            room.Color = input.Color;
            room.X = input.X; room.Z = input.Z;
            room.Width = input.Width; room.Depth = input.Depth; room.Height = input.Height;
            await db.SaveChangesAsync();
            return Results.Ok(new RoomDto(room.Id, room.HouseId, room.Name, room.Color,
                room.X, room.Z, room.Width, room.Depth, room.Height));
        });

        r.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var room = await db.Rooms.FindAsync(id);
            if (room is null) return Results.NotFound();
            db.Rooms.Remove(room);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}
