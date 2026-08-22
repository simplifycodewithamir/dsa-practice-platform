using System.Net;
using System.Net.Http.Json;
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
    public async Task CreateSubmission_ValidRequest_PersistsAndReturns201WithLocation()
    {
        var question = await SeedQuestionAsync();
        using var client = factory.CreateClient();
        var request = new CreateSubmissionRequest(question.Id, "user-1", "csharp", "Console.WriteLine(1);");

        using var response = await client.PostAsJsonAsync("/api/v1/submissions", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<SubmissionResponse>();
        Assert.Equal("Pending", created!.Status);
        Assert.Equal(question.Id, created.QuestionId);
        Assert.Equal($"/api/v1/submissions/{created.Id}", response.Headers.Location!.OriginalString);

        using var getResponse = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<SubmissionResponse>();
        Assert.Equal(created.Id, fetched!.Id);
    }

    [Fact]
    public async Task CreateSubmission_UnknownQuestionId_Returns404()
    {
        using var client = factory.CreateClient();
        var request = new CreateSubmissionRequest(Guid.NewGuid(), "user-1", "csharp", "Console.WriteLine(1);");

        using var response = await client.PostAsJsonAsync("/api/v1/submissions", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("api.error.notfound", problemDetails!.Title);
    }

    [Fact]
    public async Task CreateSubmission_UnsupportedLanguage_Returns400()
    {
        var question = await SeedQuestionAsync();
        using var client = factory.CreateClient();
        var request = new CreateSubmissionRequest(question.Id, "user-1", "rust", "fn main() {}");

        using var response = await client.PostAsJsonAsync("/api/v1/submissions", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("api.error.badrequest", problemDetails!.Title);
    }

    [Fact]
    public async Task GetSubmissionById_UnknownId_Returns404()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/v1/submissions/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("api.error.notfound", problemDetails!.Title);
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
