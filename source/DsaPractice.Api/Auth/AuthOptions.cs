namespace DsaPractice.Api.Auth;

internal sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Whether a submission requires a signed-in caller.
    ///
    /// False until item 20 stands up a real identity provider: the browser has nowhere to get a
    /// token from before then, so enforcing it now would only mean nobody can submit. Production
    /// configuration sets it to true, and item 20 makes that the default once login exists.
    /// </summary>
    public bool RequireAuthentication { get; init; }

    /// <summary>
    /// Issuer recorded for submissions made without a token, while the flag above is off. It is a
    /// marker for "nobody signed in on this machine", not an identity.
    /// </summary>
    public string LocalIssuer { get; init; } = "local-development";
}
