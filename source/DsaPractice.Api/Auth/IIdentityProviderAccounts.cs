namespace DsaPractice.Api.Auth;

/// <summary>
/// Deleting a person's account at the identity provider.
///
/// An interface with one method, because the provider-specific half of erasure is the only part of
/// this application that knows Auth0 exists by name. Keeping it behind this seam is what decision D3
/// buys: swapping provider is a new implementation and a configuration change, not a rewrite of the
/// account endpoints, and a checkout with no tenant gets <see cref="UnconfiguredIdentityProviderAccounts"/>
/// without anything downstream branching on it.
/// </summary>
internal interface IIdentityProviderAccounts
{
    /// <summary>
    /// Removes the provider's own record of this person, so that erasure covers their sign-in and
    /// not only the data stored here.
    /// </summary>
    /// <param name="issuer">The <c>iss</c> on the user row -- which tenant vouched for them.</param>
    /// <param name="subject">The <c>sub</c> on the user row, which is the provider's user id.</param>
    /// <remarks>
    /// Does not throw for a provider that refuses or is unreachable: the local data is already gone
    /// by the time this is called, so there is no operation left to abandon. The outcome is returned
    /// instead, and the caller reports it.
    /// </remarks>
    Task<IdentityProviderDeletionOutcome> DeleteUserAsync(string issuer, string subject, CancellationToken cancellationToken);
}

/// <summary>What happened at the provider. Reported to the caller rather than logged and forgotten.</summary>
internal enum IdentityProviderDeletionOutcome
{
    /// <summary>The provider confirmed the user is gone, including "was already gone".</summary>
    Deleted,

    /// <summary>
    /// Nothing was attempted: no management credentials are configured, or this user was vouched for
    /// by a different issuer than the configured tenant.
    /// </summary>
    NotAttempted,

    /// <summary>It was attempted and did not succeed. Their sign-in at the provider still exists.</summary>
    Failed
}
