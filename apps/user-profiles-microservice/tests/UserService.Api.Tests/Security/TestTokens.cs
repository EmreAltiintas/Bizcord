using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace UserService.Api.Tests.Security;

public static class TestTokens
{
    public const string SigningKey = "test-signing-key-for-unit-tests-only-0123456789abcdef";

    public static string Create(
        Guid? subject = null,
        string? role = null,
        string? scope = null,
        string? signingKey = null,
        DateTime? expires = null)
    {
        var claims = new List<Claim>();
        if (subject is { } sub)
        {
            claims.Add(new Claim("sub", sub.ToString()));
        }

        if (role is not null)
        {
            claims.Add(new Claim("role", role));
        }

        if (scope is not null)
        {
            claims.Add(new Claim("scope", scope));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? SigningKey));
        var expiry = expires ?? DateTime.UtcNow.AddHours(1);
        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: expiry.AddHours(-2),
            expires: expiry,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
