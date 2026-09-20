using Microsoft.Extensions.Options;

namespace DsaPractice.Api.Auth;

/// <summary>
/// Startup checks on the bearer configuration the Api validates tokens against.
///
/// The Api is a resource server only (decision D3) and configures authentication entirely from
/// <c>Authentication:Schemes:Bearer</c> (D4), which means a typo there does not fail anything --
/// it just quietly changes who gets in. These three checks turn each of those silent outcomes
/// into a refused startup:
///
/// <list type="bullet">
/// <item>enforcement on with no issuer configured: every request is a 401, forever;</item>
/// <item>enforcement on with no audience: a token minted for any other API on the same issuer
/// is accepted here -- including the identity provider's own userinfo tokens;</item>
/// <item>a symmetric signing key in production: those exist for local development and the
/// end-to-end stack, where the key is in a .env file. Anything holding it can mint an admin.</item>
/// </list>
/// </summary>
internal sealed class AuthConfigurationValidator(IConfiguration configuration, IHostEnvironment environment)
    : IValidateOptions<AuthOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthOptions options)
    {
        var bearer = configuration.GetSection(AuthOptions.BearerSchemeSection);
        var failures = new List<string>();

        var hasSymmetricKeys = bearer.GetSection("SigningKeys").GetChildren().Any();

        if (options.RequireAuthentication)
        {
            var hasAuthority = !string.IsNullOrWhiteSpace(bearer["Authority"]);

            if (!hasAuthority && !hasSymmetricKeys)
            {
                failures.Add(
                    $"{AuthOptions.SectionName}:RequireAuthentication is on, but no token issuer is configured. " +
                    $"Set {AuthOptions.BearerSchemeSection}:Authority to the identity provider (see docs/auth.md), " +
                    "or run `dotnet user-jwts create` for local development.");
            }

            var hasAudience = !string.IsNullOrWhiteSpace(bearer["Audience"])
                || bearer.GetSection("ValidAudiences").GetChildren().Any();

            if (!hasAudience)
            {
                failures.Add(
                    $"{AuthOptions.BearerSchemeSection}:ValidAudiences is empty. Without it any token the issuer " +
                    "has minted for any audience is accepted here.");
            }
        }

        if (hasSymmetricKeys && environment.IsProduction())
        {
            failures.Add(
                $"{AuthOptions.BearerSchemeSection}:SigningKeys is set in Production. Symmetric keys are for local " +
                "development and the end-to-end stack; production validates asymmetric signatures via Authority/JWKS.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
