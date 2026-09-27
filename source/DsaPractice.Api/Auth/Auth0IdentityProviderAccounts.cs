using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace DsaPractice.Api.Auth;

/// <summary>
/// Deletes a user at Auth0, through its Management API.
///
/// This is the only class in the Api that knows the provider by name, and it exists for one reason:
/// a person deleting their account here must not be left with a sign-in there (item 21). Everything
/// else about authentication stays plain OIDC configuration (decision D3).
///
/// Registered as a singleton so the management access token is fetched once and reused until it is
/// nearly expired, rather than once per deletion -- Auth0 rate-limits the token endpoint, and a
/// token good for hours does not need re-minting for a request that happens a few times a month.
/// </summary>
internal sealed class Auth0IdentityProviderAccounts(
    IHttpClientFactory httpClientFactory,
    IOptions<IdentityProviderOptions> options,
    TimeProvider timeProvider,
    ILogger<Auth0IdentityProviderAccounts> logger) : IIdentityProviderAccounts
{
    public const string HttpClientName = "auth0-management";

    /// <summary>
    /// How early a cached token is treated as expired. Covers the request being in flight when the
    /// real expiry passes, which would otherwise surface as an occasional, unreproducible 401.
    /// </summary>
    private static readonly TimeSpan ExpirySafetyMargin = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _tokenGate = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _cachedTokenExpiresAtUtc;

    public async Task<IdentityProviderDeletionOutcome> DeleteUserAsync(
        string issuer, string subject, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (!string.Equals(issuer, settings.Issuer, StringComparison.Ordinal))
        {
            // The subject is only meaningful at the tenant that minted it. Sending it to the
            // configured tenant anyway could delete a different person who happens to share an id.
            logger.LogWarning(
                "Not deleting a provider account: the user was vouched for by {Issuer}, but the "
                + "configured management tenant issues {ConfiguredIssuer}.",
                issuer, settings.Issuer);

            return IdentityProviderDeletionOutcome.NotAttempted;
        }

        try
        {
            var token = await GetAccessTokenAsync(settings, cancellationToken);

            using var client = httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(
                HttpMethod.Delete,
                new Uri($"{settings.ManagementAudience}users/{Uri.EscapeDataString(subject)}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await client.SendAsync(request, cancellationToken);

            // 404 is the desired end state reached by another route -- the account is not there.
            // Treating it as a failure would make a retried deletion report worse than the first.
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogInformation("Deleted the provider account for a user from {Issuer}.", issuer);
                return IdentityProviderDeletionOutcome.Deleted;
            }

            // Status only. A management API error body can echo back the user id that was sent.
            logger.LogError(
                "Auth0 refused to delete a user: {StatusCode}. Their local data is already deleted; "
                + "the provider account has to be removed from the dashboard.",
                (int)response.StatusCode);

            return IdentityProviderDeletionOutcome.Failed;
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException
                                             or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // The local erasure has already committed, so there is nothing to roll back and nothing
            // for the caller to retry as one operation -- which is exactly why this returns an
            // outcome instead of throwing.
            logger.LogError(
                exception,
                "Could not reach Auth0 to delete a user. Their local data is already deleted; the "
                + "provider account has to be removed from the dashboard.");

            return IdentityProviderDeletionOutcome.Failed;
        }
    }

    private async Task<string> GetAccessTokenAsync(IdentityProviderOptions settings, CancellationToken cancellationToken)
    {
        if (TryGetCachedToken(out var cached))
        {
            return cached;
        }

        await _tokenGate.WaitAsync(cancellationToken);
        try
        {
            // Re-checked inside the gate: several concurrent deletions would otherwise each mint a
            // token, and only the last one would be kept.
            if (TryGetCachedToken(out cached))
            {
                return cached;
            }

            using var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.PostAsJsonAsync(
                settings.TokenEndpoint,
                new ClientCredentialsRequest(settings.ClientId!, settings.ClientSecret!, settings.ManagementAudience),
                cancellationToken);

            // The body of a failed token request can contain the client id; the status is enough to
            // diagnose it (401 wrong secret, 403 missing delete:users grant).
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Auth0 returned {(int)response.StatusCode} for a management token request.");
            }

            var token = await response.Content.ReadFromJsonAsync<ClientCredentialsResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Auth0 returned an empty management token response.");

            _cachedToken = token.AccessToken;
            _cachedTokenExpiresAtUtc = timeProvider.GetUtcNow().AddSeconds(token.ExpiresInSeconds);

            return token.AccessToken;
        }
        finally
        {
            _tokenGate.Release();
        }
    }

    private bool TryGetCachedToken(out string token)
    {
        token = _cachedToken ?? string.Empty;
        return _cachedToken is not null
            && timeProvider.GetUtcNow() < _cachedTokenExpiresAtUtc - ExpirySafetyMargin;
    }

    private sealed record ClientCredentialsRequest(
        [property: JsonPropertyName("client_id")] string ClientId,
        [property: JsonPropertyName("client_secret")] string ClientSecret,
        [property: JsonPropertyName("audience")] string Audience)
    {
        [JsonPropertyName("grant_type")]
        public string GrantType => "client_credentials";
    }

    private sealed record ClientCredentialsResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresInSeconds);
}
