using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

public class User
{
    public int Id { get; set; }
    [Required, MaxLength(64)] public string Username { get; set; } = "";
    [Required] public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class House
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Room> Rooms { get; set; } = new();
}

public class Room
{
    public int Id { get; set; }
    public int HouseId { get; set; }
    public House? House { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    public string Color { get; set; } = "#7c3aed";

    // 3D placement (meters). Rooms are axis-aligned boxes on a floor plan.
    public double X { get; set; }
    public double Z { get; set; }
    public double Width { get; set; } = 4;
    public double Depth { get; set; } = 4;
    public double Height { get; set; } = 2.6;

    public List<Furniture> Furniture { get; set; } = new();
}

public enum FurnitureKind
{
    Cabinet, Drawer, Shelf, Wardrobe, Desk, Table, Sofa, Bed, Box, Other
}

public class Furniture
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room? Room { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    public FurnitureKind Kind { get; set; } = FurnitureKind.Cabinet;

    // Local position inside the room (0..Width / 0..Depth)
    public double X { get; set; }
    public double Z { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 0.8;
    public double Depth { get; set; } = 0.5;
    public double Height { get; set; } = 1.2;
    public double RotationY { get; set; }

    public List<Container> Containers { get; set; } = new();
    public List<Item> Items { get; set; } = new();
}

public class Container
{
    public int Id { get; set; }
    public int FurnitureId { get; set; }
    public Furniture? Furniture { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<Item> Items { get; set; } = new();
}

public class Item
{
    public int Id { get; set; }
    [Required, MaxLength(160)] public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public decimal? Value { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public DateTime? WarrantyUntil { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Notes { get; set; }
    public string TagsCsv { get; set; } = "";

    public int? RoomId { get; set; }
    public Room? Room { get; set; }
    public int? FurnitureId { get; set; }
    public Furniture? Furniture { get; set; }
    public int? ContainerId { get; set; }
    public Container? Container { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? ModelFileName { get; set; }
    public string? ModelContentType { get; set; }

    public List<ItemPhoto> Photos { get; set; } = new();
}

public class ItemPhoto
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    [Required] public string FileName { get; set; } = "";
    [Required] public string ContentType { get; set; } = "";
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
