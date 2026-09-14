using System.Text;
using System.Text.Json;
using DsaPractice.Contracts;
using DsaPractice.Judge.Execution;
using DsaPractice.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DsaPractice.Judge.Messaging;

/// <summary>
/// Consumes judge requests, runs them, publishes the result, then acknowledges.
///
/// Acknowledging last is the point: if the Judge dies mid-run, the message was never acked, so the
/// broker redelivers it to whoever is available. Combined with the outbox's at-least-once
/// publishing, a submission may be judged more than once -- which is why the result carries the
/// submission id and the Api's consumer is idempotent.
/// </summary>
public sealed class JudgeRequestConsumer(
    IRabbitMqConnection connection,
    ISandboxExecutor executor,
    IMessagePublisher publisher,
    ProcessedSubmissions processedSubmissions,
    IOptions<RabbitMqOptions> rabbitOptions,
    IOptions<JudgeOptions> judgeOptions,
    ILogger<JudgeRequestConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rabbit = rabbitOptions.Value;
        var judge = judgeOptions.Value;

        var connectionToBroker = await ConnectWithRetryAsync(stoppingToken);
        if (connectionToBroker is null)
        {
            return; // shutting down
        }

        var channel = await connectionToBroker.CreateChannelAsync(cancellationToken: stoppingToken);

        // Without this the broker would push the whole queue at one consumer, and the rest would
        // sit idle while this one worked through a backlog it had already claimed.
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: judge.Prefetch, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => HandleAsync(channel, delivery, stoppingToken);

        await channel.BasicConsumeAsync(
            rabbit.JudgeRequestedQueue,
            autoAck: false, // ack only after the result is published
            consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation(
            "Judge consuming {Queue} with prefetch {Prefetch}.", rabbit.JudgeRequestedQueue, judge.Prefetch);

        // Deliveries arrive on the client's own threads; this just keeps the service alive until
        // shutdown, then closes the channel so in-flight messages go back to the queue unacked.
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
        finally
        {
            await channel.DisposeAsync();
        }
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        SubmissionJudgeRequested? request;
        try
        {
            request = JsonSerializer.Deserialize<SubmissionJudgeRequested>(delivery.Body.Span, ContractJson.Options);
        }
        catch (JsonException exception)
        {
            // Nothing about this message will improve by retrying it.
            logger.LogError(exception, "Judge request could not be deserialized; dead-lettering it.");
            await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, cancellationToken);
            return;
        }

        if (request is null || request.SubmissionId == Guid.Empty)
        {
            logger.LogError("Judge request was empty or had no submission id; dead-lettering it.");
            await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, cancellationToken);
            return;
        }

        if (!processedSubmissions.TryMarkProcessed(request.SubmissionId))
        {
            logger.LogInformation("Submission {SubmissionId} was already judged by this instance; acking the redelivery.", request.SubmissionId);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
            return;
        }

        try
        {
            var outcome = await executor.ExecuteAsync(request, cancellationToken);
            var result = VerdictAggregator.Aggregate(request.SubmissionId, outcome);

            await PublishAsync(result, cancellationToken);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);

            logger.LogInformation(
                "Judged submission {SubmissionId}: {Verdict} ({TestCaseCount} test cases).",
                request.SubmissionId, result.Verdict, result.TestCaseResults.Count);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The Judge failed, not the submitted code. Tell the Api anyway -- a submission stuck
            // Running forever is worse for the user than an honest InternalError -- and park the
            // original on the dead-letter queue so the failure is visible rather than swallowed.
            logger.LogError(exception, "Judging submission {SubmissionId} failed; reporting InternalError.", request.SubmissionId);

            try
            {
                await PublishAsync(new SubmissionJudged(request.SubmissionId, JudgeVerdict.InternalError, []), cancellationToken);
            }
            catch (Exception publishFailure)
            {
                logger.LogError(publishFailure, "Reporting InternalError for {SubmissionId} also failed.", request.SubmissionId);
            }

            await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, cancellationToken);
        }
    }

    private async Task PublishAsync(SubmissionJudged result, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(result, ContractJson.Options);

        await publisher.PublishAsync(
            rabbitOptions.Value.JudgedRoutingKey,
            nameof(SubmissionJudged),
            result.SubmissionId.ToString(),
            body,
            cancellationToken);
    }

    /// <summary>
    /// The Judge has nothing to do without the broker, so unlike the Api it waits for one rather
    /// than failing to start.
    /// </summary>
    private async Task<IConnection?> ConnectWithRetryAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(1);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                return await connection.GetAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Cannot reach RabbitMQ; retrying in {Delay}.", delay);
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }

                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
            }
        }

        return null;
    }
}
