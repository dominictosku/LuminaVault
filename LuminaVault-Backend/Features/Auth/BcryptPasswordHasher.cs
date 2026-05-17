namespace LuminaVault.Auth;

/// Default IPasswordHasher implementation backed by BCrypt.Net-Next. Work factor
/// is the library default (11), which calibrates to ~100-300ms on modern hardware —
/// the right cost for an interactive login endpoint that's already rate-limited.
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string plain) => BCrypt.Net.BCrypt.HashPassword(plain);

    public bool Verify(string plain, string hash)
    {
        // BCrypt.Verify throws on malformed hashes (e.g. legacy SHA1 rows from a
        // half-migrated import). Treat any parse failure as a failed verification
        // rather than a 500 — the caller already maps `false` to Unauthorized.
        try { return BCrypt.Net.BCrypt.Verify(plain, hash); }
        catch { return false; }
    }
}
