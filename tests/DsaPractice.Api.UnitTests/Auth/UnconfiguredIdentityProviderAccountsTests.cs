using DsaPractice.Api.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DsaPractice.Api.UnitTests.Auth;

public class UnconfiguredIdentityProviderAccountsTests
{
    [Fact]
    public async Task DeleteUserAsync_ReportsNotAttempted_RatherThanPretendingToSucceed()
    {
        var accounts = new UnconfiguredIdentityProviderAccounts(
            NullLogger<UnconfiguredIdentityProviderAccounts>.Instance);

        var outcome = await accounts.DeleteUserAsync(
            "https://dev-tenant.us.auth0.com/", "auth0|abc", TestContext.Current.CancellationToken);

        // Reporting Deleted here would make the API claim an erasure it had not performed -- the one
        // thing a right-to-erasure endpoint must never do.
        Assert.Equal(IdentityProviderDeletionOutcome.NotAttempted, outcome);
    }
}
