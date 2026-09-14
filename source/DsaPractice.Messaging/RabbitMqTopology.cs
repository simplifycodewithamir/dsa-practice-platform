using RabbitMQ.Client;

namespace DsaPractice.Messaging;

/// <summary>
/// Declared identically by every service, on every connection. Declaration is idempotent, so
/// whichever service starts first creates it and the others agree -- but only if they agree
/// exactly: RabbitMQ rejects a redeclaration whose durability or arguments differ (406
/// PRECONDITION_FAILED), which is why this exists once rather than once per service.
/// </summary>
public static class RabbitMqTopology
{
    public static async Task DeclareAsync(IConnection connection, RabbitMqOptions options, CancellationToken cancellationToken)
    {
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            options.Exchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            options.DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: cancellationToken);

        // Work queues dead-letter to the same parking queue. A message only lands there when a
        // consumer rejects it outright (bad payload, or a failure it decided not to retry), so the
        // queue having anything in it is a signal worth looking at.
        var deadLettered = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = options.DeadLetterExchange,
            ["x-dead-letter-routing-key"] = options.DeadLetterRoutingKey
        };

        await channel.QueueDeclareAsync(
            options.JudgeRequestedQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: deadLettered, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            options.JudgeRequestedQueue, options.Exchange, options.JudgeRequestedRoutingKey, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            options.JudgedQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: deadLettered, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            options.JudgedQueue, options.Exchange, options.JudgedRoutingKey, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            options.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            options.DeadLetterQueue, options.DeadLetterExchange, options.DeadLetterRoutingKey, cancellationToken: cancellationToken);
    }
}
