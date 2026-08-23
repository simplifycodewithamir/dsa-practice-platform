namespace DsaPractice.Api.Auth;

/// <summary>
/// Resource-server JWT validation settings. Issuer/Audience are plain config; SigningKey is a
/// secret (dotnet user-secrets locally, a real secret store in any real deployment) -- never in
/// appsettings.json. See the "auth architecture" note on <see cref="AppRoles"/> for why this API
/// signs its own tokens for now instead of validating against an external OAuth2/OIDC Authority.
/// </summary>
internal sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string SigningKey { get; init; }
}
