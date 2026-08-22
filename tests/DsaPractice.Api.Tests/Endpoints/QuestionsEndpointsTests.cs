using System.Net;
using System.Net.Http.Json;
using DsaPractice.Api.DataAccess;
using DsaPractice.Api.DataAccess.Entities;
using DsaPractice.Api.Endpoints;
using DsaPractice.Api.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DsaPractice.Api.Tests.Endpoints;

[Collection(ApiTestCollection.Name)]
public class QuestionsEndpointsTests(ApiWebApplicationFactory factory)
{
    [Fact]
    public async Task GetQuestions_ReturnsSeededQuestion()
    {
        var question = await SeedQuestionAsync(withTestCases: false);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/questions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var questions = await response.Content.ReadFromJsonAsync<List<QuestionSummaryResponse>>();
        Assert.Contains(questions!, q => q.Id == question.Id && q.Title == question.Title);
    }

    [Fact]
    public async Task GetQuestionById_ExistingId_ReturnsDetailWithVisibleTestCasesOnly()
    {
        var question = await SeedQuestionAsync(withTestCases: true);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/v1/questions/{question.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<QuestionDetailResponse>();
        Assert.Equal(question.Id, detail!.Id);
        var visibleTestCase = Assert.Single(detail.TestCases);
        Assert.Equal("2 3", visibleTestCase.Input);
    }

    [Fact]
    public async Task GetQuestionById_UnknownId_Returns404WithNotFoundTitle()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/v1/questions/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("api.error.notfound", problemDetails!.Title);
    }

    private async Task<Question> SeedQuestionAsync(bool withTestCases)
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

        if (withTestCases)
        {
            question.TestCases =
            [
                new TestCase { Id = Guid.NewGuid(), QuestionId = question.Id, Input = "2 3", ExpectedOutput = "5", IsHidden = false },
                new TestCase { Id = Guid.NewGuid(), QuestionId = question.Id, Input = "10 20", ExpectedOutput = "30", IsHidden = true }
            ];
        }

        db.Questions.Add(question);
        await db.SaveChangesAsync();

        return question;
    }
}
