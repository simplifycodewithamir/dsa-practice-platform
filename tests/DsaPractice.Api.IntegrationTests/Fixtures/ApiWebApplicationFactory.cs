using DsaPractice.DataAccess;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace DsaPractice.Api.IntegrationTests.Fixtures;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Must match RabbitMqOptions' default, which is what the Api declares and publishes to.</summary>
    public const string JudgeRequestQueue = "submission.judge-requested";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    // A real broker rather than a mocked publisher: creating a submission now publishes, so every
    // test in this collection needs somewhere for that message to go.
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:3-management-alpine").Build();

    private IConnection? _rabbitMqConnection;

    /// <summary>Connection for tests that need to read what the Api published.</summary>
    public IConnection RabbitMqConnection =>
        _rabbitMqConnection ?? throw new InvalidOperationException("The fixture has not been initialized yet.");

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();
        await db.Database.EnsureCreatedAsync();

        var factory = new ConnectionFactory { Uri = new Uri(_rabbitMq.GetConnectionString()) };
        _rabbitMqConnection = await factory.CreateConnectionAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:Uri"] = _rabbitMq.GetConnectionString(),
            // Tests drive OutboxProcessor directly so they assert what a relay pass does instead of
            // racing its timer. The loop around it is covered by starting the app at all.
            ["Outbox:RelayEnabled"] = "false"
        }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<DsaPracticeDbContext>>();
            services.AddDbContext<DsaPracticeDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));
        });
    }

    public override async ValueTask DisposeAsync()
    {
        if (_rabbitMqConnection is not null)
        {
            await _rabbitMqConnection.DisposeAsync();
        }

        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }
}
