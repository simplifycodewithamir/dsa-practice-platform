using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace DsaPractice.Messaging;

public interface IMessagePublisher
{
    /// <summary>Publishes an already-serialized message, returning only once the broker confirms it.</summary>
    Task PublishAsync(string routingKey, string type, string messageId, ReadOnlyMemory<byte> body, CancellationToken cancellationToken);
}

/// <summary>
/// Deliberately knows nothing about message types: the outbox row carries the routing key, type
/// and body, and this just gets it to the broker.
/// </summary>
public sealed class RabbitMqPublisher(
    IRabbitMqConnection connection,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqPublisher> logger) : IMessagePublisher
{
    public async Task PublishAsync(string routingKey, string type, string messageId, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        var connectionToBroker = await connection.GetAsync(cancellationToken);

        // Publisher confirms: BasicPublishAsync completes only once the broker has taken
        // responsibility for the message, and throws if it nacks or the message is unroutable.
        // The relay depends on that: it must not mark a row processed for a message the broker
        // never accepted.
        await using var channel = await connectionToBroker.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        var properties = new BasicProperties
        {
            Persistent = true, // survives a broker restart, since the queue is durable too
            ContentType = "application/json",
            MessageId = messageId,
            Type = type
        };

        await channel.BasicPublishAsync(
            options.Value.Exchange,
            routingKey,
            mandatory: true, // an unroutable message is an error, not something to discard silently
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        logger.LogInformation("Published {Type} {MessageId} ({ByteCount} bytes)", type, messageId, body.Length);
    }
}
