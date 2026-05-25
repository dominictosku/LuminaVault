using System.IdentityModel.Tokens.Jwt;
using System.Text;
using LuminaVault.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LuminaVault.Auth;

public sealed record JwtStartupState(JwtOptions Options, bool LoadedFromEnvironment);

public static class AuthServiceRegistration
{
    public const string DevFallbackJwtKey = "dev-only-key-change-me-please-this-must-be-32+chars-long!!";

    public static JwtStartupState AddLuminaVaultAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = ResolveJwtOptions(configuration, out var loadedFromEnvironment);
        var setupOptions = ResolveSetupOptions(configuration);

        services.AddSingleton(jwtOptions);
        services.AddSingleton(setupOptions);
        services.AddSingleton<JwtService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                    NameClaimType = JwtRegisteredClaimNames.UniqueName
                };
                o.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async ctx =>
                    {
                        var userId = ctx.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                        var tokenStamp = ctx.Principal?.FindFirst(JwtService.SecurityStampClaim)?.Value;
                        if (!int.TryParse(userId, out var id) || string.IsNullOrWhiteSpace(tokenStamp))
                        {
                            ctx.Fail("Token is missing user identity metadata.");
                            return;
                        }

                        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                        var currentStamp = await db.Users
                            .Where(u => u.Id == id)
                            .Select(u => u.SecurityStamp)
                            .FirstOrDefaultAsync(ctx.HttpContext.RequestAborted);

                        if (currentStamp is null || !string.Equals(currentStamp, tokenStamp, StringComparison.Ordinal))
                            ctx.Fail("Token has been revoked.");
                    }
                };
            });

        services.AddAuthorization(o =>
        {
            var authenticatedUsersOnly = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
            o.DefaultPolicy = authenticatedUsersOnly;
            o.FallbackPolicy = authenticatedUsersOnly;
        });

        return new JwtStartupState(jwtOptions, loadedFromEnvironment);
    }

    public static void ValidateJwtStartup(this WebApplication app, JwtStartupState state)
    {
        if (state.Options.Key == DevFallbackJwtKey)
        {
            var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
            if (!app.Environment.IsDevelopment())
            {
                startupLogger.LogCritical(
                    "LuminaVault is running with the built-in dev JWT key in a non-Development environment. " +
                    "Set Jwt:Key in appsettings.json or the LUMINA_JWT_KEY env var before exposing this instance.");
                throw new InvalidOperationException(
                    "Refusing to start outside Development with the built-in dev JWT key. Set Jwt:Key or LUMINA_JWT_KEY.");
            }

            startupLogger.LogWarning(
                "Using built-in dev JWT key. Set Jwt:Key or LUMINA_JWT_KEY for any non-local use.");
        }
        else if (state.LoadedFromEnvironment)
        {
            app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup")
                .LogInformation("JWT signing key loaded from LUMINA_JWT_KEY env var.");
        }
    }

    private static JwtOptions ResolveJwtOptions(IConfiguration configuration, out bool loadedFromEnvironment)
    {
        var jwtOptions = new JwtOptions();
        configuration.GetSection("Jwt").Bind(jwtOptions);
        loadedFromEnvironment = false;

        if (string.IsNullOrWhiteSpace(jwtOptions.Key))
        {
            var envKey = Environment.GetEnvironmentVariable("LUMINA_JWT_KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                jwtOptions.Key = envKey;
                loadedFromEnvironment = true;
            }
            else
            {
                jwtOptions.Key = DevFallbackJwtKey;
            }
        }

        if (Encoding.UTF8.GetByteCount(jwtOptions.Key) < 32)
        {
            throw new InvalidOperationException(
                "JWT signing key must be at least 32 UTF-8 bytes. Set Jwt:Key or LUMINA_JWT_KEY to a long random value.");
        }

        return jwtOptions;
    }

    private static AuthSetupOptions ResolveSetupOptions(IConfiguration configuration)
    {
        var setupOptions = new AuthSetupOptions();
        configuration.GetSection("Setup").Bind(setupOptions);

        var envSecret = Environment.GetEnvironmentVariable("LUMINA_SETUP_SECRET");
        if (!string.IsNullOrWhiteSpace(envSecret))
            setupOptions.RegistrationSecret = envSecret;

        return setupOptions;
    }
}
