using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace DsaPractice.Api.Messaging;

internal interface IRabbitMqConnection : IAsyncDisposable
{
    Task<IConnection> GetAsync(CancellationToken cancellationToken);
}

/// <summary>
/// One long-lived connection for the process, opened on first use rather than at startup: the Api
/// serves questions perfectly well while the broker is down, so a broker outage shouldn't stop it
/// booting. The client's own automatic recovery handles reconnects after that.
/// Topology is declared once per connection, so a publish never races an undeclared queue.
/// </summary>
internal sealed class RabbitMqConnection(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConnection> logger)
    : IRabbitMqConnection
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public async Task<IConnection> GetAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }

            var factory = new ConnectionFactory
            {
                Uri = new Uri(options.Value.Uri),
                AutomaticRecoveryEnabled = true,
                ClientProvidedName = "dsa-practice-api"
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            logger.LogInformation("Connected to RabbitMQ at {Endpoint}", _connection.Endpoint);

            await DeclareTopologyAsync(_connection, options.Value, cancellationToken);

            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Declared by the publisher and (from item 9) by the consumer too -- both are idempotent, and
    /// whichever side starts first makes the queue exist, so no message is published into a void.
    /// </summary>
    private static async Task DeclareTopologyAsync(IConnection connection, RabbitMqOptions options, CancellationToken cancellationToken)
    {
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            options.Exchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            options.JudgeRequestedQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            options.JudgeRequestedQueue,
            options.Exchange,
            options.JudgeRequestedRoutingKey,
            cancellationToken: cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }
}
