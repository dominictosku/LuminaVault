using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace LuminaVault.Auth;

/// RFC 6238 TOTP (HMAC-SHA1, 6 digits, 30s period) — the scheme every authenticator app
/// (Google Authenticator, Authy, 1Password, …) implements. Kept self-contained rather than
/// pulling in a third-party OTP dependency; the maths is verified against the RFC 6238
/// reference test vectors in the test suite.
public sealed class TotpService
{
    private const int Digits = 6;
    private const int DigitsModulo = 1_000_000;
    private const int PeriodSeconds = 30;
    private const int SecretBytes = 20; // 160-bit, the size SHA-1 authenticators expect

    private static readonly char[] Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".ToCharArray();

    public string GenerateSecret() => Base32Encode(RandomNumberGenerator.GetBytes(SecretBytes));

    public string BuildOtpAuthUri(string secret, string account, string issuer)
    {
        var label = Uri.EscapeDataString($"{issuer}:{account}");
        var query =
            $"secret={secret}" +
            $"&issuer={Uri.EscapeDataString(issuer)}" +
            $"&algorithm=SHA1&digits={Digits}&period={PeriodSeconds}";
        return $"otpauth://totp/{label}?{query}";
    }

    /// Accepts a code from the current 30s step plus `window` steps either side, tolerating
    /// clock drift and the user typing as a step rolls over.
    public bool VerifyCode(string secret, string? code, int window = 1)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code)) return false;
        code = code.Trim();
        if (code.Length != Digits || !code.All(char.IsDigit)) return false;

        byte[] key;
        try { key = Base32Decode(secret); }
        catch (FormatException) { return false; }

        var step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / PeriodSeconds;
        var match = false;
        // Check every candidate (no early return) so timing doesn't reveal which step matched.
        for (var offset = -window; offset <= window; offset++)
            match |= CodesEqual(ComputeCode(key, step + offset), code);
        return match;
    }

    public string ComputeCode(byte[] key, long counter)
    {
        Span<byte> message = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(message, counter);

        Span<byte> hash = stackalloc byte[20]; // SHA-1 digest length
        HMACSHA1.HashData(key, message, hash);

        var offset = hash[^1] & 0x0f;
        var binary =
            ((hash[offset] & 0x7f) << 24) |
            (hash[offset + 1] << 16) |
            (hash[offset + 2] << 8) |
            hash[offset + 3];
        return (binary % DigitsModulo).ToString().PadLeft(Digits, '0');
    }

    private static bool CodesEqual(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));

    // RFC 4648 base32, uppercase, unpadded — the secret format authenticator apps consume.
    public static string Base32Encode(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder((data.Length + 4) / 5 * 8);
        int buffer = 0, bitsLeft = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                sb.Append(Base32Alphabet[(buffer >> bitsLeft) & 0x1f]);
            }
        }
        if (bitsLeft > 0)
            sb.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 0x1f]);
        return sb.ToString();
    }

    public static byte[] Base32Decode(string secret)
    {
        var cleaned = secret.Trim().TrimEnd('=').Replace(" ", "").ToUpperInvariant();
        var output = new List<byte>(cleaned.Length * 5 / 8);
        int buffer = 0, bitsLeft = 0;
        foreach (var c in cleaned)
        {
            var value = Array.IndexOf(Base32Alphabet, c);
            if (value < 0) throw new FormatException($"Invalid base32 character '{c}'.");
            buffer = (buffer << 5) | value;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)((buffer >> bitsLeft) & 0xff));
            }
        }
        return output.ToArray();
    }
}
