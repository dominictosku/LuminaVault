using LuminaVault.Auth;
using LuminaVault.Data;
using LuminaVault.Domain;
using LuminaVault.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

public record AuthRequest(string Username, string Password);
public record AuthResponse(string Token, string Username);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth").WithTags("Auth");

        g.MapGet("/status", async (AppDbContext db) =>
        {
            var hasUser = await db.Users.AnyAsync();
            return Results.Ok(new { hasUser });
        });

        g.MapPost("/register", async ([FromBody] AuthRequest req, AppDbContext db, JwtService jwt, IPasswordHasher hasher) =>
        {
            if (await db.Users.AnyAsync())
                return Problem.Conflict("A user already exists. This is a single-user app.");
            if (string.IsNullOrWhiteSpace(req.Username) || req.Password.Length < 6)
                return Problem.BadRequest("Username required and password must be at least 6 chars.");

            var user = new User
            {
                Username = req.Username.Trim(),
                PasswordHash = hasher.Hash(req.Password)
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return Results.Ok(new AuthResponse(jwt.Issue(user), user.Username));
        }).RequireRateLimiting("auth");

        g.MapPost("/login", async ([FromBody] AuthRequest req, AppDbContext db, JwtService jwt, IPasswordHasher hasher) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username);
            if (user is null || !hasher.Verify(req.Password, user.PasswordHash))
                return Results.Unauthorized();
            return Results.Ok(new AuthResponse(jwt.Issue(user), user.Username));
        }).RequireRateLimiting("auth");

        g.MapPost("/change-password", async (
            HttpContext ctx,
            [FromBody] ChangePasswordRequest req,
            AppDbContext db,
            JwtService jwt,
            IPasswordHasher hasher) =>
        {
            var username = ctx.User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username)) return Results.Unauthorized();
            if (req.NewPassword.Length < 8)
                return Problem.BadRequest("New password must be at least 8 chars.");

            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user is null || !hasher.Verify(req.CurrentPassword, user.PasswordHash))
                return Problem.BadRequest("Current password is incorrect.");

            user.PasswordHash = hasher.Hash(req.NewPassword);
            await db.SaveChangesAsync();
            return Results.Ok(new AuthResponse(jwt.Issue(user), user.Username));
        }).RequireAuthorization();

        return app;
    }
}
