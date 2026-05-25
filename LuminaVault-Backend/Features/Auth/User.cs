using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

public class User
{
    public int Id { get; set; }
    [Required, MaxLength(64)] public string Username { get; set; } = "";
    [Required] public string PasswordHash { get; set; } = "";
    [Required, MaxLength(64)] public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
