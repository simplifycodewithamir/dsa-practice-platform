using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DsaPractice.Api.Auth;

internal interface IJwtTokenService
{
    string CreateToken(string userId, string role);
}

/// <summary>
/// Mints tokens for <see cref="Endpoints.AuthEndpoints"/>'s Development-only dev-token endpoint.
/// Claim names/types here have to line up exactly with the <see cref="TokenValidationParameters"/>
/// configured in Program.cs (NameClaimType = sub, RoleClaimType = "role") -- see the comment there.
/// </summary>
internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : IJwtTokenService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    public string CreateToken(string userId, string role)
    {
        var jwtOptions = options.Value;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim("role", role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            notBefore: now,
            expires: now.Add(TokenLifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
