namespace LuminaVault.Auth;

/// Indirection over the chosen password-hashing primitive so AuthEndpoints doesn't
/// import a vendor namespace. BCrypt is the current default; swapping to Argon2id
/// (the modern OWASP recommendation) only requires a new implementation and one DI
/// registration change, not edits to every call site.
public interface IPasswordHasher
{
    /// Returns a self-describing hash (algorithm + work factor + salt + digest)
    /// suitable for storing in the User.PasswordHash column.
    string Hash(string plain);

    /// Constant-time-ish verification — returns true when the supplied plaintext
    /// produces the stored hash. False on any mismatch or malformed hash.
    bool Verify(string plain, string hash);
}
