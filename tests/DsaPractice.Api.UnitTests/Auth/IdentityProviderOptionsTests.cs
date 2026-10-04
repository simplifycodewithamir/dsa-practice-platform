using DsaPractice.Api.Auth;
using Xunit;

namespace DsaPractice.Api.UnitTests.Auth;

/// <summary>
/// Whether erasure may call the provider at all, and what it derives from the domain.
///
/// The half-configured case matters most: treating it as "not configured" would turn a typo in one
/// setting into an erasure that silently skips the provider, which is the failure nobody notices.
/// </summary>
public class IdentityProviderOptionsTests
{
    private static IdentityProviderOptions Options(string? domain, string? clientId, string? clientSecret) =>
        new() { Domain = domain, ClientId = clientId, ClientSecret = clientSecret };

    [Fact]
    public void IsConfigured_WithAllThreeSettings_IsTrue()
    {
        var options = Options("dev-tenant.us.auth0.com", "client", "secret");

        Assert.True(options.IsConfigured);
        Assert.False(options.IsPartiallyConfigured);
    }

    [Fact]
    public void IsConfigured_WithNothingSet_IsFalse()
    {
        var options = Options(null, null, null);

        // The default, and what every checkout with no tenant of its own runs as.
        Assert.False(options.IsConfigured);
        Assert.False(options.IsPartiallyConfigured);
    }

    [Theory]
    [InlineData("dev-tenant.us.auth0.com", "client", null)]
    [InlineData("dev-tenant.us.auth0.com", null, "secret")]
    [InlineData(null, "client", "secret")]
    [InlineData("dev-tenant.us.auth0.com", null, null)]
    [InlineData("   ", "client", "secret")]
    public void IsPartiallyConfigured_WithSomeSettingsMissing_IsTrue(string? domain, string? clientId, string? clientSecret)
    {
        var options = Options(domain, clientId, clientSecret);

        Assert.False(options.IsConfigured);
        // Program.cs refuses to start on this rather than quietly running without the provider.
        Assert.True(options.IsPartiallyConfigured);
    }

    [Fact]
    public void DerivedUrls_ComeFromTheDomain()
    {
        var options = Options("dev-s4cf7y7mvj0ejgti.us.auth0.com", "client", "secret");

        // The trailing slash is what the provider actually puts in `iss`; without it no user row
        // would ever match and every deletion would report NotAttempted.
        Assert.Equal("https://dev-s4cf7y7mvj0ejgti.us.auth0.com/", options.Issuer);
        Assert.Equal("https://dev-s4cf7y7mvj0ejgti.us.auth0.com/api/v2/", options.ManagementAudience);
        Assert.Equal(new Uri("https://dev-s4cf7y7mvj0ejgti.us.auth0.com/oauth/token"), options.TokenEndpoint);
    }
}
