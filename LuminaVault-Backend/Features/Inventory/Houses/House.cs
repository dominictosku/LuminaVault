using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

public class House
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Room> Rooms { get; set; } = new();
}
