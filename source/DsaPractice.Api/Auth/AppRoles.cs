namespace DsaPractice.Api.Auth;

/// <summary>
/// Auth architecture (resource-server pattern): this API validates OAuth2-issued JWT bearer
/// tokens on protected endpoints -- it does not implement a full OAuth2 authorization server
/// itself (authorization-code flow, consent, client registration, ...), which is normally a
/// dedicated IdP's job (Auth0, Entra ID, Keycloak, ...). No such IdP is stood up for this solo
/// side project yet, so <see cref="Endpoints.AuthEndpoints"/> mints locally-signed dev tokens
/// instead, Development-only. Swapping to a real Authority later means: point <see
/// cref="JwtOptions"/> at the IdP's issuer/audience, switch to asymmetric JWKS validation instead
/// of a shared signing key, and delete the dev-token endpoint -- everything downstream (the
/// policy/filter authorization checks, the roles below) is unaffected, since it only depends on
/// claims already being present on a validated token, never on how that token was issued.
/// </summary>
internal static class AppRoles
{
    public const string User = "User";
    public const string Admin = "Admin";
}
