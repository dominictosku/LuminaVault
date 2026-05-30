using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

public class User
{
    public int Id { get; set; }
    [Required, MaxLength(64)] public string Username { get; set; } = "";
    [Required] public string PasswordHash { get; set; } = "";
    [Required, MaxLength(64)] public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// TOTP two-factor. The Base32 secret is written when enrolment starts and only treated
    /// as active once TwoFactorEnabled flips true (after the user proves possession with a
    /// valid code). RecoveryCodes holds newline-joined SHA-256 hashes of the unused codes.
    public bool TwoFactorEnabled { get; set; }
    [MaxLength(64)] public string? TwoFactorSecret { get; set; }
    public string? TwoFactorRecoveryCodes { get; set; }
}
