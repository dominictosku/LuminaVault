using LuminaVault.Auth;
using LuminaVault.Data;
using LuminaVault.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Endpoints;

public record AuthRequest(string Username, string Password);
public record AuthResponse(string Token, string Username);

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

        g.MapPost("/register", async ([FromBody] AuthRequest req, AppDbContext db, JwtService jwt) =>
        {
            if (await db.Users.AnyAsync())
                return Results.Conflict(new { error = "A user already exists. This is a single-user app." });
            if (string.IsNullOrWhiteSpace(req.Username) || req.Password.Length < 6)
                return Results.BadRequest(new { error = "Username required and password must be at least 6 chars." });

            var user = new User
            {
                Username = req.Username.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return Results.Ok(new AuthResponse(jwt.Issue(user), user.Username));
        });

        g.MapPost("/login", async ([FromBody] AuthRequest req, AppDbContext db, JwtService jwt) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username);
            if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                return Results.Unauthorized();
            return Results.Ok(new AuthResponse(jwt.Issue(user), user.Username));
        });

        return app;
    }
}
