using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

public class User
{
    public int Id { get; set; }
    [Required, MaxLength(64)] public string Username { get; set; } = "";
    [Required] public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
