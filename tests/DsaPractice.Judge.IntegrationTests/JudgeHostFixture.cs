using DsaPractice.Judge.Execution;
using DsaPractice.Judge.Messaging;
using DsaPractice.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace DsaPractice.Judge.IntegrationTests;

/// <summary>
/// Runs the real Judge host against a real broker: the consumer's behaviour -- prefetch, acking
/// only after publishing, dead-lettering what it rejects -- is broker behaviour, and a mock would
/// assert nothing about it.
/// </summary>
public sealed class JudgeHostFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:3-management-alpine").Build();
    private IHost? _host;

    /// <summary>Queue and exchange names only -- the host gets its own bound instance.</summary>
    public RabbitMqOptions Options { get; } = new() { Uri = "unused" };
    public IConnection Connection { get; private set; } = null!;

    /// <summary>Swapped per test to control what "running" a submission produces.</summary>
    public StubSandboxExecutor Executor { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _rabbitMq.StartAsync();
        Connection = await new ConnectionFactory { Uri = new Uri(_rabbitMq.GetConnectionString()) }.CreateConnectionAsync();

        // The host only declares the topology when its consumer first connects, which happens on a
        // background thread after StartAsync returns. Every test purges these queues before it runs,
        // and purging a queue that does not exist yet is a 404 that kills the channel -- so declare
        // it here instead of racing the host. Declaration is idempotent; the host agrees with what
        // it finds.
        await RabbitMqTopology.DeclareAsync(Connection, Options, CancellationToken.None);

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        // Bound from configuration rather than set in code: RabbitMqOptions.Uri is `required init`,
        // which is what stops a service starting without one.
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:Uri"] = _rabbitMq.GetConnectionString()
        });
        builder.Services.AddOptions<RabbitMqOptions>().Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
        builder.Services.AddOptions<JudgeOptions>();
        builder.Services.AddSingleton(new RabbitMqClientName("judge-tests"));
        builder.Services.AddSingleton<IRabbitMqConnection, RabbitMqConnection>();
        builder.Services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
        builder.Services.AddSingleton(new ProcessedSubmissions());
        builder.Services.AddSingleton<ISandboxExecutor>(Executor);
        builder.Services.AddHostedService<JudgeRequestConsumer>();

        _host = builder.Build();
        await _host.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        await Connection.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class JudgeTestCollection : ICollectionFixture<JudgeHostFixture>
{
    public const string Name = "Judge integration tests";
}
