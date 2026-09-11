using System.Net;
using System.Net.Http.Json;
using DsaPractice.Api.DataAccess.Entities;
using DsaPractice.Api.Endpoints;
using DsaPractice.Api.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DsaPractice.Api.IntegrationTests.Endpoints;

[Collection(ApiTestCollection.Name)]
public class QuestionsEndpointsTests(ApiWebApplicationFactory factory)
{
    [Fact]
    public async Task GetQuestions_ReturnsSeededQuestionWithSlugDifficultyAndTags()
    {
        var question = await TestData.SeedAsync(factory, TestData.NewQuestion(q =>
        {
            q.Difficulty = QuestionDifficulty.Medium;
            q.Tags = ["array", "hashing"];
        }));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/questions", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var questions = await response.Content.ReadFromJsonAsync<List<QuestionSummaryResponse>>(TestJson.Options, TestContext.Current.CancellationToken);
        var summary = Assert.Single(questions!, q => q.Id == question.Id);
        Assert.Equal(question.Slug, summary.Slug);
        Assert.Equal(QuestionDifficulty.Medium, summary.Difficulty);
        Assert.Equal(["array", "hashing"], summary.Tags);
    }

    [Fact]
    public async Task GetQuestionBySlug_ExistingSlug_ReturnsDetailWithSampleTestCasesOnlyInOrder()
    {
        var question = TestData.NewQuestion(q =>
        {
            q.TimeLimitMs = 2000;
            q.MemoryLimitMb = 128;
        });
        // Inserted out of order on purpose -- the response must be ordered by Ordinal, not insertion.
        question.TestCases =
        [
            TestData.NewTestCase(question.Id, ordinal: 2, input: "10 20", expectedOutput: "30"),
            TestData.NewTestCase(question.Id, ordinal: 3, input: "-1 1", expectedOutput: "0", isHidden: true),
            TestData.NewTestCase(question.Id, ordinal: 1, input: "2 3", expectedOutput: "5")
        ];
        await TestData.SeedAsync(factory, question);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/v1/questions/{question.Slug}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<QuestionDetailResponse>(TestJson.Options, TestContext.Current.CancellationToken);
        Assert.Equal(question.Id, detail!.Id);
        Assert.Equal(question.Slug, detail.Slug);
        Assert.Equal(2000, detail.TimeLimitMs);
        Assert.Equal(128, detail.MemoryLimitMb);
        Assert.Equal([1, 2], detail.SampleTestCases.Select(tc => tc.Ordinal));
        Assert.Equal("2 3", detail.SampleTestCases[0].Input);
    }

    [Fact]
    public async Task GetQuestionBySlug_UnknownSlug_Returns404WithNotFoundTitle()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/v1/questions/no-such-question-{Guid.NewGuid():N}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("api.error.notfound", problemDetails!.Title);
    }

    [Fact]
    public async Task GetQuestionBySlug_MalformedSlug_Returns404WithNotFoundTitle()
    {
        using var client = factory.CreateClient();

        // The slug regex route constraint fails here -- no endpoint runs, nothing throws, so this
        // never reaches GlobalExceptionHandler. It's UseStatusCodePages's ProblemDetails path
        // (Program.cs) that has to produce the "api.error.notfound" title instead of the
        // framework's default reason-phrase title ("Not Found").
        using var response = await client.GetAsync("/api/v1/questions/not_a_slug", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("api.error.notfound", problemDetails!.Title);
    }
}
