using System.Security.Cryptography;
using System.Text;

namespace LuminaVault.Auth;

/// One-time backup codes for when the authenticator device is lost. Codes are stored only as
/// SHA-256 hashes, newline-joined; a fast hash is appropriate because the codes are
/// high-entropy random tokens (unlike low-entropy passwords, which need BCrypt's slowness).
public static class RecoveryCodes
{
    private const int Count = 10;
    private const int CodeChars = 10;
    private const char Separator = '\n';
    // No easily-confused characters (no 0/O, 1/I/L) so codes are safe to read off paper.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// Returns the plaintext codes to show the user once (grouped `XXXXX-XXXXX` for legibility)
    /// plus the newline-joined hashes to persist.
    public static (IReadOnlyList<string> Plaintext, string Stored) Generate()
    {
        var plaintext = new List<string>(Count);
        var hashes = new List<string>(Count);
        for (var i = 0; i < Count; i++)
        {
            var raw = NewRaw();
            plaintext.Add(Format(raw));
            hashes.Add(Hash(raw));
        }
        return (plaintext, string.Join(Separator, hashes));
    }

    /// Consumes a matching unused code from `stored` (mutating it to drop the used one) and
    /// returns true. Tolerant of the dashes/spacing/case the user might type or omit.
    public static bool TryConsume(ref string? stored, string? input)
    {
        if (string.IsNullOrWhiteSpace(stored) || string.IsNullOrWhiteSpace(input)) return false;

        var target = Hash(Canonicalize(input));
        var remaining = stored.Split(Separator, StringSplitOptions.RemoveEmptyEntries).ToList();

        var matchIndex = -1;
        for (var i = 0; i < remaining.Count; i++)
        {
            // Compare every entry (no early break) to avoid leaking the position via timing.
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(remaining[i]), Encoding.ASCII.GetBytes(target)))
                matchIndex = i;
        }
        if (matchIndex < 0) return false;

        remaining.RemoveAt(matchIndex);
        stored = remaining.Count == 0 ? null : string.Join(Separator, remaining);
        return true;
    }

    public static int RemainingCount(string? stored) =>
        string.IsNullOrWhiteSpace(stored)
            ? 0
            : stored.Split(Separator, StringSplitOptions.RemoveEmptyEntries).Length;

    private static string NewRaw()
    {
        var chars = new char[CodeChars];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }

    private static string Format(string raw) => $"{raw[..5]}-{raw[5..]}";

    private static string Canonicalize(string input) =>
        new string(input.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private static string Hash(string canonicalCode) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalCode)));
}
