using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LuminaVault.Domain;
using Microsoft.IdentityModel.Tokens;

namespace LuminaVault.Auth;

public class JwtOptions
{
    public string Issuer { get; set; } = "lumina-vault";
    public string Audience { get; set; } = "lumina-vault";
    public string Key { get; set; } = "";
    public int ExpiryHours { get; set; } = 24 * 30;
}

public class JwtService
{
    private readonly JwtOptions _opt;
    public JwtService(JwtOptions opt) { _opt = opt; }

    public string Issue(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_opt.ExpiryHours),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
