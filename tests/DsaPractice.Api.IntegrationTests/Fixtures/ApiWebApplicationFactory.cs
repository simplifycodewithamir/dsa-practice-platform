using System.Net.Http.Headers;
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

    /// <summary>
    /// A client that is signed in as <paramref name="subject"/>, defaulting to someone nobody else
    /// in the suite is. Submissions require a token since item 20, so this is what most tests need;
    /// <see cref="WebApplicationFactory{TEntryPoint}.CreateClient()" /> is still the anonymous caller.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string? subject = null, string? name = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.For(subject ?? $"user-{Guid.NewGuid():N}", name));

        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:Uri"] = _rabbitMq.GetConnectionString(),
            // Same shape `dotnet user-jwts` writes for local development (decision D4), so the Api
            // is configured here exactly as it is on a developer's machine.
            ["Authentication:Schemes:Bearer:ValidIssuer"] = TestTokens.Issuer,
            ["Authentication:Schemes:Bearer:ValidAudiences:0"] = TestTokens.Audience,
            ["Authentication:Schemes:Bearer:SigningKeys:0:Id"] = "test",
            // Issuer and Length are part of the shape the framework's configuration binder expects;
            // without the Issuer it silently binds no keys at all and every token fails validation.
            ["Authentication:Schemes:Bearer:SigningKeys:0:Issuer"] = TestTokens.Issuer,
            ["Authentication:Schemes:Bearer:SigningKeys:0:Value"] = TestTokens.SigningKeyBase64,
            ["Authentication:Schemes:Bearer:SigningKeys:0:Length"] = "32",
            // Tests drive OutboxProcessor directly so they assert what a relay pass does instead of
            // racing its timer. The loop around it is covered by starting the app at all.
            ["Outbox:RelayEnabled"] = "false",
            // Explicit rather than relying on the default: enforcement being on is the thing most
            // of these tests are written against, so it belongs in the fixture where it is visible.
            ["Auth:RequireAuthentication"] = "true"
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
