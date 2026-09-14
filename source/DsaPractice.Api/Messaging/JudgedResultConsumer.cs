using System.Text.Json;
using DsaPractice.Contracts;
using DsaPractice.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DsaPractice.Api.Messaging;

/// <summary>
/// Consumes the Judge's results and applies them to submissions. Acks only after the transaction
/// commits, so a crash mid-handling means the broker redelivers rather than the result being lost.
/// </summary>
internal sealed class JudgedResultConsumer(
    IRabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<JudgedResultConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionToBroker = await ConnectWithRetryAsync(stoppingToken);
        if (connectionToBroker is null)
        {
            return;
        }

        var channel = await connectionToBroker.CreateChannelAsync(cancellationToken: stoppingToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => HandleAsync(channel, delivery, stoppingToken);

        await channel.BasicConsumeAsync(
            options.Value.JudgedQueue, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        logger.LogInformation("Api consuming {Queue}.", options.Value.JudgedQueue);

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
        SubmissionJudged? result;
        try
        {
            result = JsonSerializer.Deserialize<SubmissionJudged>(delivery.Body.Span, ContractJson.Options);
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Judged result could not be deserialized; dead-lettering it.");
            await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, cancellationToken);
            return;
        }

        if (result is null || result.SubmissionId == Guid.Empty)
        {
            logger.LogError("Judged result was empty or had no submission id; dead-lettering it.");
            await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, cancellationToken);
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var recorder = scope.ServiceProvider.GetRequiredService<JudgedResultRecorder>();

            var outcome = await recorder.RecordAsync(result, cancellationToken);

            // A result for a submission that doesn't exist is acked, not dead-lettered: there is
            // nothing to fix and nothing to retry, and parking it would only add noise.
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);

            if (outcome == RecordOutcome.UnknownSubmission)
            {
                logger.LogWarning("Discarded a result for submission {SubmissionId}, which does not exist.", result.SubmissionId);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Database trouble is usually temporary, so this one does go back to the queue.
            logger.LogError(exception, "Recording the result for {SubmissionId} failed; requeueing it.", result.SubmissionId);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, cancellationToken);
        }
    }

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
                logger.LogWarning(exception, "Cannot reach RabbitMQ to consume results; retrying in {Delay}.", delay);
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
