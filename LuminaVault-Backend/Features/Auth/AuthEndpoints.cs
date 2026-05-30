using LuminaVault.Auth;
using LuminaVault.Data;
using LuminaVault.Domain;
using LuminaVault.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

public record AuthRequest(
    string? Username,
    string? Password,
    string? SetupSecret = null,
    string? TotpCode = null,
    string? RecoveryCode = null);
public record AuthResponse(string Token, string Username);
public record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth").WithTags("Auth");

        g.MapGet("/status", async (AppDbContext db, AuthSetupOptions setup, IHostEnvironment env) =>
        {
            var hasUser = await db.Users.AnyAsync();
            return Results.Ok(new
            {
                hasUser,
                requiresSetupSecret = !hasUser && setup.RequiresRegistrationSecret(env)
            });
        }).AllowAnonymous();

        g.MapPost("/register", async (
            [FromBody] AuthRequest req,
            AppDbContext db,
            JwtService jwt,
            IPasswordHasher hasher,
            AuthSetupOptions setup,
            IHostEnvironment env) =>
        {
            if (await db.Users.AnyAsync())
                return Problem.Conflict("A user already exists. This is a single-user app.");
            if (setup.RequiresRegistrationSecret(env))
            {
                if (!setup.HasRegistrationSecret)
                    return Problem.BadRequest("Set LUMINA_SETUP_SECRET before registering the first user.");
                if (!setup.VerifyRegistrationSecret(req.SetupSecret))
                    return Problem.BadRequest("Setup secret is incorrect.");
            }
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrEmpty(req.Password) || req.Password.Length < 8)
                return Problem.BadRequest("Username required and password must be at least 8 chars.");

            var user = new User
            {
                Username = req.Username.Trim(),
                PasswordHash = hasher.Hash(req.Password)
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return Results.Ok(new AuthResponse(jwt.Issue(user), user.Username));
        }).AllowAnonymous().RequireRateLimiting("auth");

        g.MapPost("/login", async ([FromBody] AuthRequest req, AppDbContext db, JwtService jwt, IPasswordHasher hasher, TotpService totp) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrEmpty(req.Password))
                return Results.Unauthorized();

            var username = req.Username.Trim();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user is null || !hasher.Verify(req.Password, user.PasswordHash))
                return Results.Unauthorized();

            if (user.TwoFactorEnabled)
            {
                if (!string.IsNullOrWhiteSpace(req.TotpCode))
                {
                    if (!totp.VerifyCode(user.TwoFactorSecret ?? "", req.TotpCode))
                        return TwoFactorChallenge("Invalid authentication code.");
                }
                else if (!string.IsNullOrWhiteSpace(req.RecoveryCode))
                {
                    var codes = user.TwoFactorRecoveryCodes;
                    if (!RecoveryCodes.TryConsume(ref codes, req.RecoveryCode))
                        return TwoFactorChallenge("Invalid recovery code.");
                    user.TwoFactorRecoveryCodes = codes;
                    await db.SaveChangesAsync();
                }
                else
                {
                    // Password was correct but a second factor is needed; the client prompts for it.
                    return TwoFactorChallenge(null);
                }
            }

            return Results.Ok(new AuthResponse(jwt.Issue(user), user.Username));
        }).AllowAnonymous().RequireRateLimiting("login");

        g.MapPost("/change-password", async (
            HttpContext ctx,
            [FromBody] ChangePasswordRequest req,
            AppDbContext db,
            JwtService jwt,
            IPasswordHasher hasher) =>
        {
            var username = ctx.User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username)) return Results.Unauthorized();
            if (string.IsNullOrEmpty(req.CurrentPassword) || string.IsNullOrEmpty(req.NewPassword))
                return Problem.BadRequest("Current password and new password are required.");
            if (req.NewPassword.Length < 8)
                return Problem.BadRequest("New password must be at least 8 chars.");

            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user is null || !hasher.Verify(req.CurrentPassword, user.PasswordHash))
                return Problem.BadRequest("Current password is incorrect.");

            user.PasswordHash = hasher.Hash(req.NewPassword);
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync();
            return Results.Ok(new AuthResponse(jwt.Issue(user), user.Username));
        }).RequireAuthorization();

        return app;
    }

    // A correct password but a missing/invalid second factor. 401 with a `twoFactorRequired`
    // flag the login page keys off to switch into code-entry mode; the HTTP interceptor leaves
    // /auth/ 401s alone, so this doesn't bounce the user out.
    private static IResult TwoFactorChallenge(string? error) =>
        Results.Json(new { twoFactorRequired = true, error }, statusCode: StatusCodes.Status401Unauthorized);
}
