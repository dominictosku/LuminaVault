using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

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
