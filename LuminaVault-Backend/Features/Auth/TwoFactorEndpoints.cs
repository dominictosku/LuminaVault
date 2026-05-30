using LuminaVault.Auth;
using LuminaVault.Data;
using LuminaVault.Domain;
using LuminaVault.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

public record TwoFactorStatusResponse(bool Enabled, int RecoveryCodesRemaining);
public record TwoFactorSetupResponse(string Secret, string OtpauthUri);
public record TwoFactorEnableRequest(string? Code);
public record TwoFactorEnableResponse(string[] RecoveryCodes);
public record TwoFactorDisableRequest(string? Password);

public static class TwoFactorEndpoints
{
    private const string Issuer = "LuminaVault";

    public static IEndpointRouteBuilder MapTwoFactor(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth/2fa").RequireAuthorization().WithTags("TwoFactor");

        g.MapGet("/status", async (HttpContext ctx, AppDbContext db) =>
        {
            var user = await CurrentUser(ctx, db);
            return user is null
                ? Results.Unauthorized()
                : Results.Ok(new TwoFactorStatusResponse(
                    user.TwoFactorEnabled,
                    RecoveryCodes.RemainingCount(user.TwoFactorRecoveryCodes)));
        });

        // Begin enrolment: mint a fresh secret and hand back the otpauth:// URI for the QR.
        // Not active until /enable proves the user can produce a valid code from it.
        g.MapPost("/setup", async (HttpContext ctx, AppDbContext db, TotpService totp) =>
        {
            var user = await CurrentUser(ctx, db);
            if (user is null) return Results.Unauthorized();
            if (user.TwoFactorEnabled) return Problem.BadRequest("Two-factor authentication is already enabled.");

            var secret = totp.GenerateSecret();
            user.TwoFactorSecret = secret;
            await db.SaveChangesAsync();

            return Results.Ok(new TwoFactorSetupResponse(secret, totp.BuildOtpAuthUri(secret, user.Username, Issuer)));
        });

        g.MapPost("/enable", async (HttpContext ctx, [FromBody] TwoFactorEnableRequest req, AppDbContext db, TotpService totp) =>
        {
            var user = await CurrentUser(ctx, db);
            if (user is null) return Results.Unauthorized();
            if (user.TwoFactorEnabled) return Problem.BadRequest("Two-factor authentication is already enabled.");
            if (string.IsNullOrWhiteSpace(user.TwoFactorSecret)) return Problem.BadRequest("Start setup before enabling.");
            if (!totp.VerifyCode(user.TwoFactorSecret, req.Code))
                return Problem.BadRequest("That code didn't match. Check your authenticator's clock and try again.");

            var (plaintext, stored) = RecoveryCodes.Generate();
            user.TwoFactorEnabled = true;
            user.TwoFactorRecoveryCodes = stored;
            await db.SaveChangesAsync();

            return Results.Ok(new TwoFactorEnableResponse(plaintext.ToArray()));
        });

        // Disabling is a sensitive change, so it re-checks the password rather than relying on
        // the existing session alone.
        g.MapPost("/disable", async (HttpContext ctx, [FromBody] TwoFactorDisableRequest req, AppDbContext db, IPasswordHasher hasher) =>
        {
            var user = await CurrentUser(ctx, db);
            if (user is null) return Results.Unauthorized();
            if (!user.TwoFactorEnabled) return Problem.BadRequest("Two-factor authentication is not enabled.");
            if (string.IsNullOrEmpty(req.Password) || !hasher.Verify(req.Password, user.PasswordHash))
                return Problem.BadRequest("Password is incorrect.");

            user.TwoFactorEnabled = false;
            user.TwoFactorSecret = null;
            user.TwoFactorRecoveryCodes = null;
            await db.SaveChangesAsync();

            return Results.Ok(new TwoFactorStatusResponse(false, 0));
        });

        return app;
    }

    private static async Task<User?> CurrentUser(HttpContext ctx, AppDbContext db)
    {
        var username = ctx.User.Identity?.Name;
        return string.IsNullOrWhiteSpace(username)
            ? null
            : await db.Users.FirstOrDefaultAsync(u => u.Username == username);
    }
}
