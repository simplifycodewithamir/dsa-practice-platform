using System.Text.Json;
using DsaPractice.Contracts;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace DsaPractice.Api.Messaging;

internal interface IJudgeRequestPublisher
{
    Task PublishAsync(SubmissionJudgeRequested message, CancellationToken cancellationToken);
}

internal sealed class JudgeRequestPublisher(
    IRabbitMqConnection connection,
    IOptions<RabbitMqOptions> options,
    ILogger<JudgeRequestPublisher> logger) : IJudgeRequestPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync(SubmissionJudgeRequested message, CancellationToken cancellationToken)
    {
        var rabbit = options.Value;
        var connectionToBroker = await connection.GetAsync(cancellationToken);

        // Publisher confirms: BasicPublishAsync completes only once the broker has taken
        // responsibility for the message, and throws if it nacks or the message is unroutable.
        // Without this, publishing is fire-and-forget and a dropped message looks like success.
        await using var channel = await connectionToBroker.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        var properties = new BasicProperties
        {
            Persistent = true, // survives a broker restart, since the queue is durable too
            ContentType = "application/json",
            MessageId = message.SubmissionId.ToString(),
            Type = nameof(SubmissionJudgeRequested)
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);

        await channel.BasicPublishAsync(
            rabbit.Exchange,
            rabbit.JudgeRequestedRoutingKey,
            mandatory: true, // an unroutable message is an error, not something to discard silently
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Published judge request for submission {SubmissionId} ({TestCaseCount} test cases, {ByteCount} bytes)",
            message.SubmissionId, message.TestCases.Count, body.Length);
    }
}
