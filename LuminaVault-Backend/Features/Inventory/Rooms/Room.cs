using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

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
