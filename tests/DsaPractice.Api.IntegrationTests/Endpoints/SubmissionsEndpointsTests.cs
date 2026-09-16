using DsaPractice.DataAccess.Enums;
using System.Net;
using System.Net.Http.Json;
using DsaPractice.DataAccess.Entities;
using DsaPractice.Api.Endpoints;
using DsaPractice.Api.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DsaPractice.Api.IntegrationTests.Endpoints;

[Collection(ApiTestCollection.Name)]
public class SubmissionsEndpointsTests(ApiWebApplicationFactory factory)
{
    [Fact]
    public async Task CreateSubmission_ValidRequest_PersistsAndReturns201WithLocation()
    {
        var question = await TestData.SeedAsync(factory, TestData.NewQuestion());
        using var client = factory.CreateClient();
        var request = new CreateSubmissionRequest(question.Id, "csharp", "Console.WriteLine(1);");

        using var response = await client.PostAsJsonAsync("/api/v1/submissions", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<SubmissionResponse>(TestJson.Options, TestContext.Current.CancellationToken);
        Assert.Equal(SubmissionStatus.Pending, created!.Status);
        Assert.Null(created.Verdict); // no verdict until the Judge has run it
        Assert.Equal(question.Id, created.QuestionId);
        Assert.Equal($"/api/v1/submissions/{created.Id}", response.Headers.Location!.OriginalString);

        using var getResponse = await client.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<SubmissionResponse>(TestJson.Options, TestContext.Current.CancellationToken);
        Assert.Equal(created.Id, fetched!.Id);
    }

    [Fact]
    public async Task CreateSubmission_UnknownQuestionId_Returns404()
    {
        using var client = factory.CreateClient();
        var request = new CreateSubmissionRequest(Guid.NewGuid(), "csharp", "Console.WriteLine(1);");

        using var response = await client.PostAsJsonAsync("/api/v1/submissions", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("api.error.notfound", problemDetails!.Title);
    }

    [Fact]
    public async Task CreateSubmission_UnsupportedLanguage_Returns400()
    {
        var question = await TestData.SeedAsync(factory, TestData.NewQuestion());
        using var client = factory.CreateClient();
        var request = new CreateSubmissionRequest(question.Id, "rust", "fn main() {}");

        using var response = await client.PostAsJsonAsync("/api/v1/submissions", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("api.error.badrequest", problemDetails!.Title);
    }

    [Fact]
    public async Task GetSubmissionById_UnknownId_Returns404()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/v1/submissions/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("api.error.notfound", problemDetails!.Title);
    }
}
