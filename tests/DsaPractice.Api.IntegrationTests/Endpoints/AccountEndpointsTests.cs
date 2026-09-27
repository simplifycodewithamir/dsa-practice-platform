using System.Net;
using System.Net.Http.Json;
using DsaPractice.Api.Auth;
using DsaPractice.Api.Endpoints;
using DsaPractice.Api.IntegrationTests.Fixtures;
using DsaPractice.DataAccess;
using DsaPractice.DataAccess.Entities;
using DsaPractice.DataAccess.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DsaPractice.Api.IntegrationTests.Endpoints;

/// <summary>
/// "My account" (item 21): reading my own history, and erasing myself.
///
/// The erasure tests assert on what is left in the database rather than on the response alone --
/// a right-to-erasure endpoint that returns the right JSON while leaving rows behind is the exact
/// failure that matters, and it is invisible from the HTTP surface.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class AccountEndpointsTests(ApiWebApplicationFactory factory)
{
    [Theory]
    [InlineData("GET", "/api/v1/me")]
    [InlineData("GET", "/api/v1/me/submissions")]
    [InlineData("DELETE", "/api/v1/me")]
    public async Task EveryRoute_WithoutAToken_Returns401(string method, string path)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EveryRoute_WithEnforcementOff_StillRequiresAToken()
    {
        // Submissions fall back to a shared local user when Auth:RequireAuthentication is off. These
        // routes deliberately do not: "me" with nobody signed in has no defensible answer, and an
        // unauthenticated DELETE would erase that shared row in the configuration where a developer
        // is least expecting it.
        using var anonymousFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Auth:RequireAuthentication"] = "false" })));

        using var client = anonymousFactory.CreateClient();

        using var account = await client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken);
        using var deletion = await client.DeleteAsync("/api/v1/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, account.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, deletion.StatusCode);
    }

    [Fact]
    public async Task GetAccount_ReturnsTheCallersOwnAccount()
    {
        var subject = $"account-{Guid.NewGuid():N}";
        using var client = factory.CreateAuthenticatedClient(subject, name: "Grace");

        var account = await GetAsync<AccountResponse>(client, "/api/v1/me");

        Assert.Equal("Grace", account.DisplayName);
        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.NotEqual(default, account.CreatedAtUtc);

        // Provisioned by this very request: someone who has signed in but never submitted gets their
        // account, not a 404 they can do nothing about.
        var stored = await FindUserAsync(subject);
        Assert.Equal(stored!.Id, account.Id);
    }

    [Fact]
    public async Task GetSubmissions_ReturnsOnlyMine_NewestFirst()
    {
        var question = await TestData.SeedAsync(factory, TestData.NewQuestion());
        using var mine = factory.CreateAuthenticatedClient($"mine-{Guid.NewGuid():N}");
        using var theirs = factory.CreateAuthenticatedClient($"theirs-{Guid.NewGuid():N}");

        var first = await SubmitAsync(mine, question.Id);
        var second = await SubmitAsync(mine, question.Id);
        var somebodyElses = await SubmitAsync(theirs, question.Id);

        var page = await GetAsync<AccountSubmissionsResponse>(mine, "/api/v1/me/submissions");

        Assert.Equal(2, page.TotalCount);
        Assert.Equal([second.Id, first.Id], page.Items.Select(i => i.Id));
        Assert.DoesNotContain(somebodyElses.Id, page.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task GetSubmissions_CarriesTheQuestionSlugAndTitle_WithoutTheSourceCode()
    {
        var question = await TestData.SeedAsync(factory, TestData.NewQuestion());
        using var client = factory.CreateAuthenticatedClient();
        await SubmitAsync(client, question.Id);

        var page = await GetAsync<AccountSubmissionsResponse>(client, "/api/v1/me/submissions");

        var row = Assert.Single(page.Items);
        // So the list can link to the problem without a request per row.
        Assert.Equal(question.Slug, row.QuestionSlug);
        Assert.Equal(question.Title, row.QuestionTitle);
        Assert.Equal(SubmissionStatus.Pending, row.Status);
        Assert.Null(row.Verdict);
    }

    [Fact]
    public async Task GetSubmissions_PagesThroughTheHistory()
    {
        var question = await TestData.SeedAsync(factory, TestData.NewQuestion());
        using var client = factory.CreateAuthenticatedClient($"paged-{Guid.NewGuid():N}");

        var older = await SubmitAsync(client, question.Id);
        var newer = await SubmitAsync(client, question.Id);

        var firstPage = await GetAsync<AccountSubmissionsResponse>(client, "/api/v1/me/submissions?page=1&pageSize=1");
        var secondPage = await GetAsync<AccountSubmissionsResponse>(client, "/api/v1/me/submissions?page=2&pageSize=1");

        // The total is what tells the UI there is a next page; a full page cannot imply it.
        Assert.Equal(2, firstPage.TotalCount);
        Assert.Equal(newer.Id, Assert.Single(firstPage.Items).Id);
        Assert.Equal(older.Id, Assert.Single(secondPage.Items).Id);
        Assert.Equal(2, secondPage.Page);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?page=-1")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    public async Task GetSubmissions_WithPagingOutOfRange_Returns400(string query)
    {
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.GetAsync($"/api/v1/me/submissions{query}", TestContext.Current.CancellationToken);

        // Rejected, not clamped: serving page 1 to a client that asked for page 0 hides its bug.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("api.error.badrequest", problem!.Title);
    }

    [Fact]
    public async Task DeleteAccount_ErasesTheSubmissionsTheResultsAndTheQueuedCode()
    {
        var question = await TestData.SeedAsync(factory, TestData.NewQuestion());
        var subject = $"erased-{Guid.NewGuid():N}";
        using var client = factory.CreateAuthenticatedClient(subject);

        var submission = await SubmitAsync(client, question.Id);
        await SeedTestResultAsync(submission.Id);

        var deletion = await DeleteAsync<DeleteAccountResponse>(client, "/api/v1/me");

        Assert.Equal(1, deletion.SubmissionsDeleted);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.False(await db.Users.AnyAsync(u => u.Subject == subject, cancellationToken));
        Assert.False(await db.Submissions.AnyAsync(s => s.Id == submission.Id, cancellationToken));
        Assert.False(await db.SubmissionTestResults.AnyAsync(r => r.SubmissionId == submission.Id, cancellationToken));
        // The outbox row's payload is the judge request, which carries the source code verbatim.
        // Leaving it would mean the code survives the account, and gets run after it.
        Assert.False(await db.OutboxMessages.AnyAsync(m => m.MessageId == submission.Id.ToString(), cancellationToken));
    }

    [Fact]
    public async Task DeleteAccount_WithNoManagementCredentials_SaysTheProviderWasNotAttempted()
    {
        using var client = factory.CreateAuthenticatedClient();

        var deletion = await DeleteAsync<DeleteAccountResponse>(client, "/api/v1/me");

        // The fixture configures no IdentityProvider:Management section, like every checkout with no
        // tenant of its own. Claiming Deleted here would be the API lying about an erasure.
        Assert.Equal(IdentityProviderDeletionOutcome.NotAttempted, deletion.IdentityProviderAccount);
    }

    [Fact]
    public async Task DeleteAccount_LeavesEveryoneElseAlone()
    {
        var question = await TestData.SeedAsync(factory, TestData.NewQuestion());
        using var mine = factory.CreateAuthenticatedClient($"leaving-{Guid.NewGuid():N}");
        var theirSubject = $"staying-{Guid.NewGuid():N}";
        using var theirs = factory.CreateAuthenticatedClient(theirSubject);

        await SubmitAsync(mine, question.Id);
        var theirSubmission = await SubmitAsync(theirs, question.Id);

        await DeleteAsync<DeleteAccountResponse>(mine, "/api/v1/me");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.True(await db.Users.AnyAsync(u => u.Subject == theirSubject, cancellationToken));
        Assert.True(await db.Submissions.AnyAsync(s => s.Id == theirSubmission.Id, cancellationToken));
        // The question is shared content, not the leaver's -- and its foreign key restricts deletes.
        Assert.True(await db.Questions.AnyAsync(q => q.Id == question.Id, cancellationToken));
    }

    [Fact]
    public async Task DeleteAccount_ThenComingBack_IsANewAccountWithNoHistory()
    {
        var question = await TestData.SeedAsync(factory, TestData.NewQuestion());
        var subject = $"returning-{Guid.NewGuid():N}";
        using var client = factory.CreateAuthenticatedClient(subject);

        await SubmitAsync(client, question.Id);
        var before = await GetAsync<AccountResponse>(client, "/api/v1/me");
        await DeleteAsync<DeleteAccountResponse>(client, "/api/v1/me");

        // The same person at the provider, signing in again: provisioned from scratch, so the local
        // id differs and nothing they wrote before is reachable.
        var after = await GetAsync<AccountResponse>(client, "/api/v1/me");
        var history = await GetAsync<AccountSubmissionsResponse>(client, "/api/v1/me/submissions");

        Assert.NotEqual(before.Id, after.Id);
        Assert.Equal(0, history.TotalCount);
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options, TestContext.Current.CancellationToken))!;
    }

    private static async Task<T> DeleteAsync<T>(HttpClient client, string path)
    {
        using var response = await client.DeleteAsync(path, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options, TestContext.Current.CancellationToken))!;
    }

    private static async Task<SubmissionResponse> SubmitAsync(HttpClient client, Guid questionId)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/submissions",
            new CreateSubmissionRequest(questionId, "python", "print(1)"),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SubmissionResponse>(TestJson.Options, TestContext.Current.CancellationToken))!;
    }

    private async Task SeedTestResultAsync(Guid submissionId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();

        db.SubmissionTestResults.Add(new SubmissionTestResult
        {
            Id = Guid.NewGuid(),
            SubmissionId = submissionId,
            TestCaseId = Guid.NewGuid(),
            Ordinal = 1,
            Passed = true,
            ActualOutput = "1",
            ExecutionTimeMs = 12
        });

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<User?> FindUserAsync(string subject)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(
            u => u.Subject == subject, TestContext.Current.CancellationToken);
    }
}
