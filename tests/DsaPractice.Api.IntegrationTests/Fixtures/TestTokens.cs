using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace DsaPractice.Api.IntegrationTests.Fixtures;

/// <summary>
/// Mints bearer tokens for tests.
///
/// This lives in the test project on purpose: the Api validates tokens and never issues them
/// (decision D4), so there is no token-minting code in the application to ship by accident.
/// </summary>
internal static class TestTokens
{
    public const string Issuer = "dsa-practice-tests";
    public const string Audience = "dsa-practice-api";

    /// <summary>Test-only key. Nothing signed with it is trusted anywhere else.</summary>
    public const string SigningKeyBase64 = "dGVzdC1vbmx5LXNpZ25pbmcta2V5LWZvci1kc2EtcHJhY3RpY2UtaW50ZWc=";

    public static string For(string subject, string? name = null)
    {
        var key = new SymmetricSecurityKey(Convert.FromBase64String(SigningKeyBase64));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, subject) };
        if (name is not null)
        {
            claims.Add(new Claim("name", name));
        }

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
