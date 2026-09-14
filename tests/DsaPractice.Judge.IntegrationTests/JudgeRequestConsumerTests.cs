using System.Text.Json;
using DsaPractice.Contracts;
using DsaPractice.Judge.Execution;
using RabbitMQ.Client;
using Xunit;

namespace DsaPractice.Judge.IntegrationTests;

[Collection(JudgeTestCollection.Name)]
public class JudgeRequestConsumerTests(JudgeHostFixture fixture)
{
    [Fact]
    public async Task JudgeRequest_AllTestsPass_PublishesAcceptedAndAcksTheRequest()
    {
        await ResetAsync();
        var request = NewRequest();

        await PublishRequestAsync(request);

        var judged = await WaitForJudgedAsync(request.SubmissionId);
        Assert.Equal(JudgeVerdict.Accepted, judged.Verdict);
        Assert.Equal(2, judged.TestCaseResults.Count);
        Assert.Equal([1, 2], judged.TestCaseResults.Select(r => r.Ordinal));
        // Acked, so the broker does not hold it any more.
        Assert.Equal(0u, await QueueDepthAsync(fixture.Options.JudgeRequestedQueue));
    }

    [Fact]
    public async Task JudgeRequest_FailingTestCase_PublishesThatVerdictWithPerTestResults()
    {
        await ResetAsync();
        fixture.Executor.Returns(request => new ExecutionOutcome(
        [
            new TestCaseOutcome(request.TestCases[0].TestCaseId, 1, TestCaseStatus.Passed, "one", null, 5),
            new TestCaseOutcome(request.TestCases[1].TestCaseId, 2, TestCaseStatus.WrongAnswer, "nope", null, 7)
        ]));
        var request = NewRequest();

        await PublishRequestAsync(request);

        var judged = await WaitForJudgedAsync(request.SubmissionId);
        Assert.Equal(JudgeVerdict.WrongAnswer, judged.Verdict);
        Assert.Collection(judged.TestCaseResults,
            first => Assert.True(first.Passed),
            second => Assert.Equal("nope", second.ActualOutput));
    }

    [Fact]
    public async Task JudgeRequest_ExecutorThrows_ReportsInternalErrorAndDeadLettersTheRequest()
    {
        await ResetAsync();
        fixture.Executor.Throws(new InvalidOperationException("sandbox exploded"));
        var request = NewRequest();

        await PublishRequestAsync(request);

        // The user gets an honest verdict instead of a submission stuck Running forever...
        var judged = await WaitForJudgedAsync(request.SubmissionId);
        Assert.Equal(JudgeVerdict.InternalError, judged.Verdict);
        // ...and the original is parked for a human rather than silently dropped or retried forever.
        Assert.Equal(1u, await WaitForQueueDepthAsync(fixture.Options.DeadLetterQueue, 1));
    }

    [Fact]
    public async Task JudgeRequest_MalformedPayload_IsDeadLetteredWithoutJudging()
    {
        await ResetAsync();

        await PublishRawAsync("{ this is not a judge request "u8.ToArray());

        Assert.Equal(1u, await WaitForQueueDepthAsync(fixture.Options.DeadLetterQueue, 1));
        Assert.Equal(0, fixture.Executor.ExecutionCount); // nothing was run
        Assert.Equal(0u, await QueueDepthAsync(fixture.Options.JudgedQueue));
    }

    [Fact]
    public async Task JudgeRequest_DeliveredTwice_IsOnlyExecutedOnce()
    {
        await ResetAsync();
        var request = NewRequest();

        await PublishRequestAsync(request);
        await WaitForJudgedAsync(request.SubmissionId);
        await PublishRequestAsync(request); // the same submission again, as at-least-once delivery allows

        // Give the redelivery time to be consumed and acked.
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(1, fixture.Executor.ExecutionCount);
        Assert.Equal(0u, await QueueDepthAsync(fixture.Options.JudgeRequestedQueue));
    }

    private async Task ResetAsync()
    {
        fixture.Executor.Reset();
        await using var channel = await fixture.Connection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        foreach (var queue in new[] { fixture.Options.JudgeRequestedQueue, fixture.Options.JudgedQueue, fixture.Options.DeadLetterQueue })
        {
            await channel.QueuePurgeAsync(queue, TestContext.Current.CancellationToken);
        }
    }

    private static SubmissionJudgeRequested NewRequest() => new(
        Guid.NewGuid(), Guid.NewGuid(), "python", "print(1)", 1000, 256,
        [
            new JudgeTestCase(Guid.NewGuid(), 1, "1", "one"),
            new JudgeTestCase(Guid.NewGuid(), 2, "2", "two")
        ]);

    private Task PublishRequestAsync(SubmissionJudgeRequested request) =>
        PublishRawAsync(JsonSerializer.SerializeToUtf8Bytes(request, ContractJson.Options));

    private async Task PublishRawAsync(byte[] body)
    {
        await using var channel = await fixture.Connection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        await channel.BasicPublishAsync(
            fixture.Options.Exchange,
            fixture.Options.JudgeRequestedRoutingKey,
            mandatory: true,
            basicProperties: new BasicProperties { Persistent = true, ContentType = "application/json" },
            body: body,
            cancellationToken: TestContext.Current.CancellationToken);
    }

    private async Task<SubmissionJudged> WaitForJudgedAsync(Guid submissionId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            await using var channel = await fixture.Connection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
            var result = await channel.BasicGetAsync(fixture.Options.JudgedQueue, autoAck: true, TestContext.Current.CancellationToken);
            if (result is not null)
            {
                var judged = JsonSerializer.Deserialize<SubmissionJudged>(result.Body.Span, ContractJson.Options)!;
                if (judged.SubmissionId == submissionId)
                {
                    return judged;
                }
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        throw new InvalidOperationException($"No result was published for submission '{submissionId}'.");
    }

    private async Task<uint> QueueDepthAsync(string queue)
    {
        await using var channel = await fixture.Connection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        return await channel.MessageCountAsync(queue, TestContext.Current.CancellationToken);
    }

    private async Task<uint> WaitForQueueDepthAsync(string queue, uint expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        uint depth;
        do
        {
            depth = await QueueDepthAsync(queue);
            if (depth == expected)
            {
                return depth;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
        while (DateTime.UtcNow < deadline);

        return depth;
    }
}
