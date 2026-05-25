using System.Security.Cryptography;
using System.Text;

namespace LuminaVault.Auth;

public sealed class AuthSetupOptions
{
    public string RegistrationSecret { get; set; } = "";
    public bool RequireRegistrationSecretOutsideDevelopment { get; set; } = true;

    public bool HasRegistrationSecret => !string.IsNullOrWhiteSpace(RegistrationSecret);

    public bool RequiresRegistrationSecret(IHostEnvironment environment) =>
        HasRegistrationSecret ||
        (RequireRegistrationSecretOutsideDevelopment && !environment.IsDevelopment());

    public bool VerifyRegistrationSecret(string? candidate)
    {
        if (!HasRegistrationSecret || string.IsNullOrEmpty(candidate))
            return false;

        var expected = Encoding.UTF8.GetBytes(RegistrationSecret);
        var actual = Encoding.UTF8.GetBytes(candidate);
        return actual.Length == expected.Length &&
               CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
