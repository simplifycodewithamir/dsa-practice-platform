using DsaPractice.DataAccess;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace DsaPractice.ContentSeeding.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // The real migrations, not EnsureCreated -- these tests care about the check constraints.
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public DsaPracticeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DsaPracticeDbContext>()
            .UseNpgsql(
                _postgres.GetConnectionString(),
                npgsql => npgsql.MigrationsAssembly("DsaPractice.DataMigrations.Postgres"))
            .Options;

        return new DsaPracticeDbContext(options);
    }

    /// <summary>Tests share one database, so each needs its own slug (the column is unique).</summary>
    public string UniqueSlug() => $"question-{Guid.NewGuid():N}";

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class SeederTestCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Content seeding integration tests";
}
