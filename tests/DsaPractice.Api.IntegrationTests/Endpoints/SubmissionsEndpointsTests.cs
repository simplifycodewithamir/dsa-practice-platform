using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DsaPractice.Api.Auth;
using DsaPractice.Api.DataAccess;
using DsaPractice.Api.DataAccess.Entities;
using DsaPractice.Api.Endpoints;
using DsaPractice.Api.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DsaPractice.Api.IntegrationTests.Endpoints;

[Collection(ApiTestCollection.Name)]
public class SubmissionsEndpointsTests(ApiWebApplicationFactory factory)
{
    [Fact]
    public async Task CreateSubmission_ValidRequest_PersistsAndReturns201WithCallersUserId()
    {
        var question = await SeedQuestionAsync();
        using var client = CreateAuthenticatedClient("user-1");
        var request = new CreateSubmissionRequest(question.Id, "csharp", "Console.WriteLine(1);");

        using var response = await client.PostAsJsonAsync("/api/v1/submissions", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<SubmissionResponse>(TestContext.Current.CancellationToken);
        Assert.Equal("Pending", created!.Status);
        Assert.Equal(question.Id, created.QuestionId);
        // UserId comes from the bearer token's "sub" claim, never client-supplied request body.
        Assert.Equal("user-1", created.UserId);
        Assert.Equal($"/api/v1/submissions/{created.Id}", response.Headers.Location!.OriginalString);

        using var getResponse = await client.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<SubmissionResponse>(TestContext.Current.CancellationToken);
        Assert.Equal(created.Id, fetched!.Id);
    }

    [Fact]
    public async Task CreateSubmission_NoBearerToken_Returns401WithUnauthorizedTitle()
    {
        using var client = factory.CreateClient();
        var request = new CreateSubmissionRequest(Guid.NewGuid(), "csharp", "Console.WriteLine(1);");

        using var response = await client.PostAsJsonAsync("/api/v1/submissions", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("api.error.unauthorized", problemDetails!.Title);
    }

    [Fact]
    public async Task CreateSubmission_UnknownQuestionId_Returns404()
    {
        using var client = CreateAuthenticatedClient("user-1");
        var request = new CreateSubmissionRequest(Guid.NewGuid(), "csharp", "Console.WriteLine(1);");

        using var response = await client.PostAsJsonAsync("/api/v1/submissions", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("api.error.notfound", problemDetails!.Title);
    }

    [Fact]
    public async Task CreateSubmission_UnsupportedLanguage_Returns400()
    {
        var question = await SeedQuestionAsync();
        using var client = CreateAuthenticatedClient("user-1");
        var request = new CreateSubmissionRequest(question.Id, "rust", "fn main() {}");

        using var response = await client.PostAsJsonAsync("/api/v1/submissions", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("api.error.badrequest", problemDetails!.Title);
    }

    [Fact]
    public async Task GetSubmissionById_NoBearerToken_Returns401WithUnauthorizedTitle()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/v1/submissions/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("api.error.unauthorized", problemDetails!.Title);
    }

    [Fact]
    public async Task GetSubmissionById_UnknownId_Returns404()
    {
        using var client = CreateAuthenticatedClient("user-1");

        using var response = await client.GetAsync($"/api/v1/submissions/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("api.error.notfound", problemDetails!.Title);
    }

    [Fact]
    public async Task GetSubmissionById_Owner_Returns200()
    {
        var question = await SeedQuestionAsync();
        var owner = CreateAuthenticatedClient("owner-1");
        var submission = await CreateSubmissionAsync(owner, question.Id);

        using var response = await owner.GetAsync($"/api/v1/submissions/{submission.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        owner.Dispose();
    }

    [Fact]
    public async Task GetSubmissionById_NotOwnerNotAdmin_Returns403WithForbiddenTitle()
    {
        var question = await SeedQuestionAsync();
        using var owner = CreateAuthenticatedClient("owner-1");
        var submission = await CreateSubmissionAsync(owner, question.Id);
        using var otherUser = CreateAuthenticatedClient("someone-else");

        using var response = await otherUser.GetAsync($"/api/v1/submissions/{submission.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("api.error.forbidden", problemDetails!.Title);
    }

    [Fact]
    public async Task GetSubmissionById_AdminNotOwner_Returns200()
    {
        var question = await SeedQuestionAsync();
        using var owner = CreateAuthenticatedClient("owner-1");
        var submission = await CreateSubmissionAsync(owner, question.Id);
        using var admin = CreateAuthenticatedClient("admin-1", AppRoles.Admin);

        using var response = await admin.GetAsync($"/api/v1/submissions/{submission.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<SubmissionResponse> CreateSubmissionAsync(HttpClient client, Guid questionId)
    {
        var request = new CreateSubmissionRequest(questionId, "csharp", "Console.WriteLine(1);");
        using var response = await client.PostAsJsonAsync("/api/v1/submissions", request, TestContext.Current.CancellationToken);
        var created = await response.Content.ReadFromJsonAsync<SubmissionResponse>(TestContext.Current.CancellationToken);
        return created!;
    }

    private HttpClient CreateAuthenticatedClient(string userId, string role = AppRoles.User)
    {
        var client = factory.CreateClient();
        var tokenService = factory.Services.GetRequiredService<IJwtTokenService>();
        var accessToken = tokenService.CreateToken(userId, role);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private async Task<Question> SeedQuestionAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();

        var question = new Question
        {
            Id = Guid.NewGuid(),
            Title = $"Two Sum {Guid.NewGuid()}",
            Description = "Given an array of integers, return indices of the two numbers that add up to a target.",
            Difficulty = "Easy"
        };

        db.Questions.Add(question);
        await db.SaveChangesAsync();

        return question;
    }
}
