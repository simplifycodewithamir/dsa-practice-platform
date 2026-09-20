using DsaPractice.Api.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DsaPractice.Api.UnitTests.Auth;

/// <summary>
/// The Api configures authentication entirely from configuration (decision D4), so a typo there
/// does not fail anything -- it changes who gets in. These are the cases that must stop a startup
/// rather than be discovered in production.
/// </summary>
public class AuthConfigurationValidatorTests
{
    private const string Authority = "https://dsa-practice.eu.auth0.com/";

    [Fact]
    public void EnforcementOn_WithAnAuthorityAndAnAudience_Succeeds()
    {
        var result = Validate(
            requireAuthentication: true,
            settings: new()
            {
                ["Authentication:Schemes:Bearer:Authority"] = Authority,
                ["Authentication:Schemes:Bearer:ValidAudiences:0"] = "https://api.dsapractice.dev"
            });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void EnforcementOn_WithNoIssuerConfigured_Fails()
    {
        // Nothing would throw at runtime: every request would simply be a 401, forever.
        var result = Validate(requireAuthentication: true, settings: []);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("no token issuer is configured"));
    }

    [Fact]
    public void EnforcementOn_WithAnIssuerButNoAudience_Fails()
    {
        // Without an audience, a token the same issuer minted for a different API -- including its
        // own userinfo endpoint -- is accepted here.
        var result = Validate(
            requireAuthentication: true,
            settings: new() { ["Authentication:Schemes:Bearer:Authority"] = Authority });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("ValidAudiences is empty"));
    }

    [Fact]
    public void EnforcementOff_NeedsNoIssuer()
    {
        // The deliberate escape hatch: the end-to-end stack, and a developer poking at the Api
        // before setting an issuer up.
        var result = Validate(requireAuthentication: false, settings: []);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ASymmetricSigningKey_InProduction_Fails()
    {
        // Those keys exist for local development and the end-to-end stack, where the key sits in a
        // .env file. Anything holding one can mint itself any subject.
        var result = Validate(
            requireAuthentication: true,
            settings: new()
            {
                ["Authentication:Schemes:Bearer:SigningKeys:0:Issuer"] = "dev",
                ["Authentication:Schemes:Bearer:SigningKeys:0:Value"] = "not-a-real-key",
                ["Authentication:Schemes:Bearer:ValidAudiences:0"] = "dsa-practice-api"
            },
            environmentName: Environments.Production);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("SigningKeys is set in Production"));
    }

    [Fact]
    public void ASymmetricSigningKey_InDevelopment_IsFine()
    {
        var result = Validate(
            requireAuthentication: true,
            settings: new()
            {
                ["Authentication:Schemes:Bearer:SigningKeys:0:Issuer"] = "dev",
                ["Authentication:Schemes:Bearer:SigningKeys:0:Value"] = "not-a-real-key",
                ["Authentication:Schemes:Bearer:ValidAudiences:0"] = "dsa-practice-api"
            });

        Assert.True(result.Succeeded);
    }

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(
        bool requireAuthentication,
        Dictionary<string, string?> settings,
        string? environmentName = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var validator = new AuthConfigurationValidator(configuration, new StubEnvironment(environmentName ?? Environments.Development));

        return validator.Validate(null, new AuthOptions { RequireAuthentication = requireAuthentication });
    }

    private sealed class StubEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "DsaPractice.Api";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
