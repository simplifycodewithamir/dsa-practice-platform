namespace DsaPractice.Api.Auth;

internal sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Configuration path the bearer scheme binds itself from. Nothing here reads it to configure
    /// authentication -- <c>AddJwtBearer()</c> does that on its own (decision D4) -- but
    /// <see cref="AuthConfigurationValidator"/> reads it to refuse a startup that would leave
    /// every request unauthenticated, or trust a development key in production.
    /// </summary>
    public const string BearerSchemeSection = "Authentication:Schemes:Bearer";

    /// <summary>
    /// Whether a submission requires a signed-in caller. On by default since item 20: the browser
    /// now has somewhere to get a token from.
    ///
    /// It stays a flag rather than becoming unconditional so the end-to-end stack, and a developer
    /// poking at the Api before setting an issuer up, can turn it off deliberately -- and so that
    /// turning it off is visible in configuration rather than implied by an empty setting.
    /// </summary>
    public bool RequireAuthentication { get; init; } = true;

    /// <summary>
    /// Issuer recorded for submissions made without a token, while the flag above is off. It is a
    /// marker for "nobody signed in on this machine", not an identity.
    /// </summary>
    public string LocalIssuer { get; init; } = "local-development";
}
