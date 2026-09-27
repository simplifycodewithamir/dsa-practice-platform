using System.Net;
using System.Net.Http.Json;
using DsaPractice.Api.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace DsaPractice.Api.UnitTests.Auth;

/// <summary>
/// The provider half of erasure.
///
/// Every case here is a failure mode that must not become a lie: this runs after the local data is
/// already deleted, so the only thing it can still get wrong is what it reports.
/// </summary>
public class Auth0IdentityProviderAccountsTests
{
    private const string Domain = "dev-tenant.us.auth0.com";
    private const string Issuer = $"https://{Domain}/";
    private const string Subject = "auth0|68d0c0ffee";

    [Fact]
    public async Task DeleteUserAsync_ForAUserFromAnotherIssuer_DoesNotCallTheProvider()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("Nothing should be sent."));
        var accounts = Create(handler);

        var outcome = await accounts.DeleteUserAsync(
            "https://someone-elses-tenant.eu.auth0.com/", Subject, TestContext.Current.CancellationToken);

        // The subject means nothing at the configured tenant; sending it could delete a stranger.
        Assert.Equal(IdentityProviderDeletionOutcome.NotAttempted, outcome);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task DeleteUserAsync_WhenTheProviderDeletesTheUser_ReportsDeleted()
    {
        var handler = new RecordingHandler(Respond(HttpStatusCode.NoContent));
        var accounts = Create(handler);

        var outcome = await accounts.DeleteUserAsync(Issuer, Subject, TestContext.Current.CancellationToken);

        Assert.Equal(IdentityProviderDeletionOutcome.Deleted, outcome);

        var delete = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Delete);
        // AbsoluteUri, not ToString(): ToString() un-escapes for display, so it would pass just as
        // happily against a URL that really did carry a raw '|'. A real Auth0 subject contains one,
        // and unescaped it addresses a different path segment than the user being deleted.
        Assert.Equal($"https://{Domain}/api/v2/users/auth0%7C68d0c0ffee", delete.Uri?.AbsoluteUri);
        Assert.Equal("minted-token", delete.BearerToken);
    }

    [Fact]
    public async Task DeleteUserAsync_WhenTheUserIsAlreadyGone_ReportsDeleted()
    {
        var accounts = Create(new RecordingHandler(Respond(HttpStatusCode.NotFound)));

        var outcome = await accounts.DeleteUserAsync(Issuer, Subject, TestContext.Current.CancellationToken);

        // Already-gone is the desired end state reached another way. Calling it a failure would make
        // a second erasure report worse than the first.
        Assert.Equal(IdentityProviderDeletionOutcome.Deleted, outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]      // the machine-to-machine app lacks delete:users
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task DeleteUserAsync_WhenTheProviderRefuses_ReportsFailed(HttpStatusCode status)
    {
        var accounts = Create(new RecordingHandler(Respond(status)));

        var outcome = await accounts.DeleteUserAsync(Issuer, Subject, TestContext.Current.CancellationToken);

        Assert.Equal(IdentityProviderDeletionOutcome.Failed, outcome);
    }

    [Fact]
    public async Task DeleteUserAsync_WhenTheTokenRequestFails_ReportsFailed()
    {
        // A wrong client secret. The deletion is never attempted, and that is still a Failed
        // erasure at the provider -- not NotAttempted, which means "nothing was configured".
        var handler = new RecordingHandler(request => request.RequestUri!.AbsolutePath == "/oauth/token"
            ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
            : throw new InvalidOperationException("The delete should never be reached."));

        var outcome = await Create(handler).DeleteUserAsync(Issuer, Subject, TestContext.Current.CancellationToken);

        Assert.Equal(IdentityProviderDeletionOutcome.Failed, outcome);
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task DeleteUserAsync_WhenTheProviderIsUnreachable_ReportsFailed()
    {
        var accounts = Create(new RecordingHandler(_ => throw new HttpRequestException("No route to host.")));

        var outcome = await accounts.DeleteUserAsync(Issuer, Subject, TestContext.Current.CancellationToken);

        // It does not throw: the local data is already gone, so there is no operation left to abandon.
        Assert.Equal(IdentityProviderDeletionOutcome.Failed, outcome);
    }

    [Fact]
    public async Task DeleteUserAsync_CalledTwice_MintsOneToken()
    {
        var handler = new RecordingHandler(Respond(HttpStatusCode.NoContent));
        var accounts = Create(handler);

        await accounts.DeleteUserAsync(Issuer, Subject, TestContext.Current.CancellationToken);
        await accounts.DeleteUserAsync(Issuer, "auth0|another", TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.Requests.Count(r => r.Method == HttpMethod.Delete));
        // Auth0 rate-limits the token endpoint, and a token good for hours does not need re-minting.
        Assert.Equal(1, handler.Requests.Count(r => r.Method == HttpMethod.Post));
    }

    [Fact]
    public async Task DeleteUserAsync_OnceTheTokenIsNearlyExpired_MintsANewOne()
    {
        var handler = new RecordingHandler(Respond(HttpStatusCode.NoContent));
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-09-27T09:00:00Z"));
        var accounts = Create(handler, clock);

        await accounts.DeleteUserAsync(Issuer, Subject, TestContext.Current.CancellationToken);

        // The stub's token lasts 3600s; the safety margin discards it a minute early, so 59m 30s in
        // it is already treated as expired rather than being used for a request that would 401
        // somewhere mid-flight.
        clock.Advance(TimeSpan.FromMinutes(59) + TimeSpan.FromSeconds(30));
        await accounts.DeleteUserAsync(Issuer, Subject, TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.Requests.Count(r => r.Method == HttpMethod.Post));
    }

    /// <summary>Token endpoint always succeeds; the deletion answers <paramref name="deleteStatus"/>.</summary>
    private static Func<HttpRequestMessage, HttpResponseMessage> Respond(HttpStatusCode deleteStatus) =>
        request => request.RequestUri!.AbsolutePath == "/oauth/token"
            ? JsonResponse(new { access_token = "minted-token", expires_in = 3600 })
            : new HttpResponseMessage(deleteStatus);

    private static HttpResponseMessage JsonResponse(object body) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    private static Auth0IdentityProviderAccounts Create(RecordingHandler handler, TimeProvider? timeProvider = null)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        // disposeHandler: false -- the class disposes each client it takes from the factory (correct
        // against the real one, whose handlers are pooled), and the stub has to survive that.
        httpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handler, disposeHandler: false));

        var options = Options.Create(new IdentityProviderOptions
        {
            Domain = Domain,
            ClientId = "client-id",
            ClientSecret = "client-secret"
        });

        return new Auth0IdentityProviderAccounts(
            httpClientFactory.Object,
            options,
            timeProvider ?? new FakeTimeProvider(),
            NullLogger<Auth0IdentityProviderAccounts>.Instance);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri, request.Headers.Authorization?.Parameter));
            return Task.FromResult(respond(request));
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, Uri? Uri, string? BearerToken);
}
