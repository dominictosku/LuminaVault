using System.Text;
using LuminaVault.Auth;

namespace LuminaVault.Tests;

/// The TOTP maths is security-load-bearing, so it's pinned to the RFC 6238 reference vectors
/// (truncated to our 6 digits) and the RFC 4648 base32 vectors.
public class TotpServiceTests
{
    private readonly TotpService _totp = new();

    // RFC 6238 Appendix B uses the SHA-1 seed "12345678901234567890" and publishes 8-digit
    // codes; our codes are 6 digits, i.e. the published value mod 1_000_000 (its last 6 digits).
    [Theory]
    [InlineData(59L, "287082")]                  // T=59            -> 94287082
    [InlineData(1111111109L, "081804")]          // T=1111111109    -> 07081804
    [InlineData(1111111111L, "050471")]          // T=1111111111    -> 14050471
    [InlineData(1234567890L, "005924")]          // T=1234567890    -> 89005924
    [InlineData(2000000000L, "279037")]          // T=2000000000    -> 69279037
    [InlineData(20000000000L, "353130")]         // T=20000000000   -> 65353130
    public void ComputeCode_matches_rfc6238_vectors(long unixTime, string expected)
    {
        var key = Encoding.ASCII.GetBytes("12345678901234567890");
        var step = unixTime / 30;

        Assert.Equal(expected, _totp.ComputeCode(key, step));
    }

    [Fact]
    public void Base32_matches_rfc4648_vector_and_round_trips()
    {
        Assert.Equal("MZXW6YTBOI", TotpService.Base32Encode(Encoding.ASCII.GetBytes("foobar")));

        var random = new byte[20];
        new Random(42).NextBytes(random);
        Assert.Equal(random, TotpService.Base32Decode(TotpService.Base32Encode(random)));
    }

    [Fact]
    public void VerifyCode_accepts_the_current_code_and_rejects_a_wrong_one()
    {
        var secret = _totp.GenerateSecret();
        var key = TotpService.Base32Decode(secret);
        var step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

        Assert.True(_totp.VerifyCode(secret, _totp.ComputeCode(key, step)));
        Assert.False(_totp.VerifyCode(secret, "000000"));
        Assert.False(_totp.VerifyCode(secret, "abc"));
        Assert.False(_totp.VerifyCode(secret, null));
    }

    [Fact]
    public void VerifyCode_tolerates_one_step_of_drift_but_not_more()
    {
        var secret = _totp.GenerateSecret();
        var key = TotpService.Base32Decode(secret);
        var step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

        Assert.True(_totp.VerifyCode(secret, _totp.ComputeCode(key, step - 1)));
        Assert.True(_totp.VerifyCode(secret, _totp.ComputeCode(key, step + 1)));
        Assert.False(_totp.VerifyCode(secret, _totp.ComputeCode(key, step + 5)));
    }

    [Fact]
    public void BuildOtpAuthUri_carries_the_secret_issuer_and_account()
    {
        var uri = _totp.BuildOtpAuthUri("JBSWY3DPEHPK3PXP", "alice", "LuminaVault");

        Assert.StartsWith("otpauth://totp/", uri);
        Assert.Contains("secret=JBSWY3DPEHPK3PXP", uri);
        Assert.Contains("issuer=LuminaVault", uri);
        Assert.Contains("LuminaVault:alice", Uri.UnescapeDataString(uri));
    }
}

public class RecoveryCodesTests
{
    [Fact]
    public void Generate_makes_ten_codes_and_stores_only_hashes()
    {
        var (plaintext, stored) = RecoveryCodes.Generate();

        Assert.Equal(10, plaintext.Count);
        Assert.Equal(10, RecoveryCodes.RemainingCount(stored));
        Assert.All(plaintext, code => Assert.Matches("^[A-Z0-9]{5}-[A-Z0-9]{5}$", code));
        // The plaintext must not be recoverable from what we persist.
        Assert.All(plaintext, code => Assert.DoesNotContain(code.Replace("-", ""), stored));
    }

    [Fact]
    public void TryConsume_accepts_a_code_once_then_rejects_reuse()
    {
        var (plaintext, stored) = RecoveryCodes.Generate();
        var code = plaintext[3];

        Assert.True(RecoveryCodes.TryConsume(ref stored, code));
        Assert.Equal(9, RecoveryCodes.RemainingCount(stored));
        Assert.False(RecoveryCodes.TryConsume(ref stored, code)); // already used
        Assert.Equal(9, RecoveryCodes.RemainingCount(stored));
    }

    [Fact]
    public void TryConsume_is_lenient_about_dashes_and_case()
    {
        var (plaintext, stored) = RecoveryCodes.Generate();
        var messy = plaintext[0].Replace("-", "").ToLowerInvariant();

        Assert.True(RecoveryCodes.TryConsume(ref stored, messy));
    }

    [Fact]
    public void TryConsume_rejects_unknown_or_blank_input()
    {
        var (_, stored) = RecoveryCodes.Generate();

        Assert.False(RecoveryCodes.TryConsume(ref stored, "ZZZZZ-ZZZZZ"));
        Assert.False(RecoveryCodes.TryConsume(ref stored, ""));
        Assert.Equal(10, RecoveryCodes.RemainingCount(stored));
    }
}
