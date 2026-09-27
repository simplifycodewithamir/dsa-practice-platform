namespace DsaPractice.Api.Auth;

/// <summary>
/// Credentials for the one thing this application asks of the identity provider beyond validating a
/// token: deleting a person's account there when they delete it here (item 21).
///
/// This is deliberately separate from <c>Authentication:Schemes:Bearer</c>. That section makes the
/// Api a resource server and is all it needs to run (decision D3); this one makes it, narrowly, a
/// client of the provider's management API, and carries a secret. Leaving it unset is a supported
/// configuration -- a checkout with no tenant of its own has nothing to manage -- and then erasure
/// deletes the local data and says plainly that the provider's copy was not touched.
/// </summary>
internal sealed class IdentityProviderOptions
{
    public const string SectionName = "IdentityProvider:Management";

    /// <summary>
    /// The tenant's domain, e.g. <c>dev-s4cf7y7mvj0ejgti.us.auth0.com</c> -- host only, no scheme
    /// and no trailing slash, because both the issuer and the management base address are derived
    /// from it and a value carrying either would produce a malformed URL for one of them.
    /// </summary>
    public string? Domain { get; init; }

    /// <summary>The machine-to-machine application's client id. Not a secret, but useless alone.</summary>
    public string? ClientId { get; init; }

    /// <summary>
    /// The machine-to-machine application's client secret. Never logged, and never in
    /// appsettings.json -- user-secrets locally, environment on the VM.
    /// </summary>
    public string? ClientSecret { get; init; }

    /// <summary>
    /// Whether an account deletion can even be attempted at the provider. All three values or none:
    /// a half-filled section is a configuration mistake, and treating it as "not configured" would
    /// turn that mistake into an erasure that silently skips the provider.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Domain)
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret);

    /// <summary>
    /// Whether the section is partly filled in -- which is never intentional, and is what the
    /// options validation in Program.cs refuses to start on.
    /// </summary>
    public bool IsPartiallyConfigured =>
        !IsConfigured
        && (!string.IsNullOrWhiteSpace(Domain)
            || !string.IsNullOrWhiteSpace(ClientId)
            || !string.IsNullOrWhiteSpace(ClientSecret));

    /// <summary>
    /// The <c>iss</c> tokens from this tenant carry. A user row is only deleted at the provider when
    /// its issuer matches: the stored subject means nothing at a tenant that did not mint it, and
    /// deleting by that id at the wrong tenant could remove an unrelated person's account.
    /// </summary>
    public string Issuer => $"https://{Domain}/";

    /// <summary>Base address of the management API, which is also the audience a token for it needs.</summary>
    public string ManagementAudience => $"https://{Domain}/api/v2/";

    public Uri TokenEndpoint => new($"https://{Domain}/oauth/token");
}
