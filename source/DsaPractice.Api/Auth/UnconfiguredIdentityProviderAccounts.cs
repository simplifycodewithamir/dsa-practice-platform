namespace DsaPractice.Api.Auth;

/// <summary>
/// What erasure uses when no management credentials are configured -- the default, and what every
/// integration test and every checkout with no tenant of its own runs with.
///
/// It reports <see cref="IdentityProviderDeletionOutcome.NotAttempted"/> rather than pretending to
/// have succeeded: the local data really is deleted, the provider's copy really is not, and the
/// response says so. A no-op that returned <see cref="IdentityProviderDeletionOutcome.Deleted"/>
/// would make the API claim an erasure it had not performed.
/// </summary>
internal sealed class UnconfiguredIdentityProviderAccounts(
    ILogger<UnconfiguredIdentityProviderAccounts> logger) : IIdentityProviderAccounts
{
    public Task<IdentityProviderDeletionOutcome> DeleteUserAsync(string issuer, string subject, CancellationToken cancellationToken)
    {
        // Warning, not Information: on a deployed instance this means an erasure request was only
        // half honoured, which someone has to know about.
        logger.LogWarning(
            "Deleted local data for a user from {Issuer}, but no identity provider management credentials "
            + "are configured, so their account at the provider still exists.",
            issuer);

        return Task.FromResult(IdentityProviderDeletionOutcome.NotAttempted);
    }
}
