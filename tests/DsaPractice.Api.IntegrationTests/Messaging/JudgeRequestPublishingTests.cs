using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DsaPractice.Api.Endpoints;
using DsaPractice.Api.IntegrationTests.Fixtures;
using DsaPractice.Api.Messaging;
using DsaPractice.Contracts;
using DsaPractice.DataAccess;
using DsaPractice.DataAccess.Entities;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Xunit;

namespace DsaPractice.Api.IntegrationTests.Messaging;

/// <summary>
/// Publishing goes through a real broker: the parts worth testing here (the topology actually
/// being declared, the message landing on the bound queue, the broker confirming the publish)
/// have no meaning against a mock.
///
/// Since item 8 the Api writes to the outbox instead of publishing inline, so each test runs a
/// relay pass to get the message onto the broker. What arrives there is what these assert.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class JudgeRequestPublishingTests(ApiWebApplicationFactory factory)
{
    [Fact]
    public async Task CreateSubmission_PublishesJudgeRequestCarryingEveryTestCase()
    {
        await MessagingState.ResetAsync(factory);
        var question = await SeedQuestionWithTestCasesAsync();
        using var client = factory.CreateClient();
        var request = new CreateSubmissionRequest(question.Id, "user-1", "python", "print(1)");

        using var response = await client.PostAsJsonAsync("/api/v1/submissions", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<SubmissionResponse>(TestJson.Options, TestContext.Current.CancellationToken);
        await RunRelayPassAsync();

        var message = await ReadJudgeRequestAsync(created!.Id);
        Assert.Equal(question.Id, message.QuestionId);
        Assert.Equal("python", message.Language);
        Assert.Equal("print(1)", message.SourceCode);
        Assert.Equal(question.TimeLimitMs, message.TimeLimitMs);
        Assert.Equal(question.MemoryLimitMb, message.MemoryLimitMb);
        // Hidden cases included: the Judge runs all of them.
        Assert.Equal(3, message.TestCases.Count);
        Assert.Equal([1, 2, 3], message.TestCases.Select(tc => tc.Ordinal));
        Assert.Contains(message.TestCases, tc => tc.ExpectedOutput == "hidden-output");
    }

    [Fact]
    public async Task CreateSubmission_PublishesPersistentMessage()
    {
        await MessagingState.ResetAsync(factory);
        var question = await SeedQuestionWithTestCasesAsync();
        using var client = factory.CreateClient();
        var request = new CreateSubmissionRequest(question.Id, "user-1", "python", "print(1)");

        using var response = await client.PostAsJsonAsync("/api/v1/submissions", request, TestContext.Current.CancellationToken);
        var created = await response.Content.ReadFromJsonAsync<SubmissionResponse>(TestJson.Options, TestContext.Current.CancellationToken);
        await RunRelayPassAsync();

        var result = await GetMessageAsync(created!.Id);

        // A broker restart must not drop queued work.
        Assert.True(result.BasicProperties.Persistent);
        Assert.Equal("application/json", result.BasicProperties.ContentType);
        Assert.Equal(created.Id.ToString(), result.BasicProperties.MessageId);
    }

    [Fact]
    public async Task CreateSubmission_UnknownQuestion_PublishesNothing()
    {
        // Other tests leave pending outbox rows, and a relay pass would publish those too.
        await MessagingState.ResetAsync(factory);
        using var client = factory.CreateClient();
        var request = new CreateSubmissionRequest(Guid.NewGuid(), "user-1", "python", "print(1)");

        using var response = await client.PostAsJsonAsync("/api/v1/submissions", request, TestContext.Current.CancellationToken);
        await RunRelayPassAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(await MessagingState.TryGetMessageAsync(factory));
    }

    private async Task RunRelayPassAsync()
    {
        using var scope = factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.ProcessPendingAsync(TestContext.Current.CancellationToken);
    }

    private async Task<SubmissionJudgeRequested> ReadJudgeRequestAsync(Guid submissionId)
    {
        var result = await GetMessageAsync(submissionId);
        return JsonSerializer.Deserialize<SubmissionJudgeRequested>(result.Body.Span, TestJson.Options)
            ?? throw new InvalidOperationException("Judge request body was null.");
    }

    /// <summary>Reads the queued message for a submission, failing the test if none arrives.</summary>
    private async Task<BasicGetResult> GetMessageAsync(Guid submissionId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var result = await MessagingState.TryGetMessageAsync(factory);
            if (result is not null && result.BasicProperties.MessageId == submissionId.ToString())
            {
                return result;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        throw new InvalidOperationException($"No judge request was published for submission '{submissionId}'.");
    }



    private async Task<Question> SeedQuestionWithTestCasesAsync()
    {
        var question = TestData.NewQuestion(q =>
        {
            q.TimeLimitMs = 1500;
            q.MemoryLimitMb = 128;
        });
        question.TestCases =
        [
            TestData.NewTestCase(question.Id, 1, "1", "one"),
            TestData.NewTestCase(question.Id, 2, "2", "two"),
            TestData.NewTestCase(question.Id, 3, "3", "hidden-output", isHidden: true)
        ];

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();
        db.Questions.Add(question);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return question;
    }
}
