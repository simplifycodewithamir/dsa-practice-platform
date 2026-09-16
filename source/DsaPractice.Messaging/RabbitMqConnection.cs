using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace DsaPractice.Messaging;

public interface IRabbitMqConnection : IAsyncDisposable
{
    Task<IConnection> GetAsync(CancellationToken cancellationToken);
}

/// <summary>
/// One long-lived connection per process, opened on first use rather than at startup: a service
/// with other work to do (the Api still serves questions) shouldn't fail to boot because the
/// broker is down. The client's own automatic recovery handles reconnects after that.
/// Topology is declared once per connection, so nothing publishes into a void.
/// </summary>
public sealed class RabbitMqConnection(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqConnection> logger,
    RabbitMqClientName clientName) : IRabbitMqConnection
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
                ClientProvidedName = clientName.Value
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            logger.LogInformation("Connected to RabbitMQ at {Endpoint}", _connection.Endpoint);

            await RabbitMqTopology.DeclareAsync(_connection, options.Value, cancellationToken);

            return _connection;
        }
        finally
        {
            _gate.Release();
        }
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

/// <summary>The name this process shows up as in the broker's connection list.</summary>
public sealed record RabbitMqClientName(string Value);
