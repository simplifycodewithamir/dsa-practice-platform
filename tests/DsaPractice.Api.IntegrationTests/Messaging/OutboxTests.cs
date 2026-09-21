using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DsaPractice.Api.Endpoints;
using DsaPractice.Api.IntegrationTests.Fixtures;
using DsaPractice.Api.Messaging;
using DsaPractice.Contracts;
using DsaPractice.DataAccess;
using DsaPractice.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Xunit;

namespace DsaPractice.Api.IntegrationTests.Messaging;

/// <summary>
/// The relay is driven directly here rather than waiting on its background loop, so the tests
/// assert what a pass does instead of racing a timer. The loop itself is a while + PeriodicTimer
/// around exactly this call.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class OutboxTests(ApiWebApplicationFactory factory)
{
    [Fact]
    public async Task CreateSubmission_WritesOutboxRowAndPublishesNothingInline()
    {
        await MessagingState.ResetAsync(factory);
        var question = await SeedQuestionAsync();
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/submissions",
            new CreateSubmissionRequest(question.Id, "python", "print(1)"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<SubmissionResponse>(TestJson.Options, TestContext.Current.CancellationToken);

        // Nothing reaches the broker until the relay runs.
        Assert.Null(await MessagingState.TryGetMessageAsync(factory));

        var row = await GetOutboxRowAsync(created!.Id);
        Assert.Equal(nameof(SubmissionJudgeRequested), row.Type);
        Assert.Equal("submission.judge-requested", row.RoutingKey);
        Assert.Null(row.ProcessedAtUtc);
        Assert.Equal(0, row.AttemptCount);

        var payload = JsonSerializer.Deserialize<SubmissionJudgeRequested>(row.Payload, TestJson.Options)!;
        Assert.Equal(created.Id, payload.SubmissionId);
        Assert.Equal(3, payload.TestCases.Count);
    }

    [Fact]
    public async Task CreateSubmission_UnknownQuestion_WritesNoOutboxRow()
    {
        await MessagingState.ResetAsync(factory);
        var before = await CountOutboxRowsAsync();
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/submissions",
            new CreateSubmissionRequest(Guid.NewGuid(), "python", "print(1)"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        // The submission was never written, so neither was its message: both or neither.
        Assert.Equal(before, await CountOutboxRowsAsync());
    }

    [Fact]
    public async Task ProcessPendingAsync_PublishesPendingRowAndMarksItProcessed()
    {
        await MessagingState.ResetAsync(factory);
        var submissionId = await EnqueueAsync();

        var published = await RunRelayPassAsync();

        Assert.True(published >= 1);
        var message = await WaitForMessageAsync(submissionId);
        Assert.Equal(submissionId.ToString(), message.BasicProperties.MessageId);
        Assert.Equal(nameof(SubmissionJudgeRequested), message.BasicProperties.Type);
        Assert.True(message.BasicProperties.Persistent);

        var row = await GetOutboxRowAsync(submissionId);
        Assert.NotNull(row.ProcessedAtUtc);
        Assert.Null(row.LastError);
    }

    [Fact]
    public async Task ProcessPendingAsync_RunTwice_DoesNotPublishTheSameRowAgain()
    {
        await MessagingState.ResetAsync(factory);
        var submissionId = await EnqueueAsync();
        await RunRelayPassAsync();
        await DrainQueueAsync();

        await RunRelayPassAsync();

        // Processed rows are never looked at again.
        Assert.Null(await MessagingState.TryGetMessageAsync(factory));
    }

    [Fact]
    public async Task ProcessPendingAsync_UnroutableMessage_LeavesRowPendingWithBackoffAndError()
    {
        await MessagingState.ResetAsync(factory);
        // Nothing is bound to this routing key, so the broker returns the message and the
        // confirmed publish throws -- standing in for any publish failure.
        var submissionId = await EnqueueAsync(routingKey: "submission.nobody-is-listening");

        var published = await RunRelayPassAsync();

        Assert.Equal(0, published);
        var row = await GetOutboxRowAsync(submissionId);
        Assert.Null(row.ProcessedAtUtc);       // still pending, so it will be retried
        Assert.Equal(1, row.AttemptCount);
        Assert.NotNull(row.LastError);
        Assert.True(row.NextAttemptAtUtc > row.OccurredAtUtc); // backed off rather than spinning
    }

    [Fact]
    public async Task ProcessPendingAsync_RowNotDueYet_IsLeftAlone()
    {
        await MessagingState.ResetAsync(factory);
        var submissionId = await EnqueueAsync(nextAttemptInSeconds: 3600);

        await RunRelayPassAsync();

        Assert.Null(await MessagingState.TryGetMessageAsync(factory));
        Assert.Null((await GetOutboxRowAsync(submissionId)).ProcessedAtUtc);
    }

    [Fact]
    public async Task PurgeProcessedAsync_DeletesOnlyLongProcessedRows()
    {
        await MessagingState.ResetAsync(factory);
        var oldProcessed = await EnqueueAsync(processedDaysAgo: 30);
        var recentProcessed = await EnqueueAsync(processedDaysAgo: 1);
        var pending = await EnqueueAsync();

        using var scope = factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.PurgeProcessedAsync(TestContext.Current.CancellationToken);

        Assert.Null(await FindOutboxRowAsync(oldProcessed));
        Assert.NotNull(await FindOutboxRowAsync(recentProcessed));
        Assert.NotNull(await FindOutboxRowAsync(pending));
    }

    private async Task<int> RunRelayPassAsync()
    {
        using var scope = factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
        return await processor.ProcessPendingAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Writes an outbox row directly, standing in for whatever produced the message.</summary>
    private async Task<Guid> EnqueueAsync(
        string routingKey = "submission.judge-requested",
        int nextAttemptInSeconds = 0,
        int? processedDaysAgo = null)
    {
        var submissionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var message = new SubmissionJudgeRequested(
            submissionId, Guid.NewGuid(), "python", "print(1)", 1000, 256,
            [new JudgeTestCase(Guid.NewGuid(), 1, "1", "one")]);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = nameof(SubmissionJudgeRequested),
            RoutingKey = routingKey,
            MessageId = submissionId.ToString(),
            Payload = JsonSerializer.Serialize(message, TestJson.Options),
            OccurredAtUtc = now,
            NextAttemptAtUtc = now.AddSeconds(nextAttemptInSeconds),
            ProcessedAtUtc = processedDaysAgo is null ? null : now.AddDays(-processedDaysAgo.Value)
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return submissionId;
    }

    private async Task<OutboxMessage> GetOutboxRowAsync(Guid submissionId) =>
        await FindOutboxRowAsync(submissionId) ?? throw new InvalidOperationException($"No outbox row for '{submissionId}'.");

    private async Task<OutboxMessage?> FindOutboxRowAsync(Guid submissionId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();
        return await db.OutboxMessages
            .AsNoTracking()
            .SingleOrDefaultAsync(m => m.MessageId == submissionId.ToString(), TestContext.Current.CancellationToken);
    }

    private async Task<int> CountOutboxRowsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();
        return await db.OutboxMessages.CountAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Question> SeedQuestionAsync()
    {
        var question = TestData.NewQuestion();
        question.TestCases =
        [
            TestData.NewTestCase(question.Id, 1, "1", "one"),
            TestData.NewTestCase(question.Id, 2, "2", "two"),
            TestData.NewTestCase(question.Id, 3, "3", "three", isHidden: true)
        ];

        return await TestData.SeedAsync(factory, question);
    }

    private async Task<BasicGetResult> WaitForMessageAsync(Guid submissionId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var result = await MessagingState.TryGetMessageAsync(factory);
            if (result?.BasicProperties.MessageId == submissionId.ToString())
            {
                return result;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        throw new InvalidOperationException($"No message was published for '{submissionId}'.");
    }

    private async Task DrainQueueAsync()
    {
        while (await MessagingState.TryGetMessageAsync(factory) is not null)
        {
        }
    }


}
