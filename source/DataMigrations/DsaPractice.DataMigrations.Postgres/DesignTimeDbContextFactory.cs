using DsaPractice.Api.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DsaPractice.DataMigrations.Postgres;

// Used only by `dotnet ef` at design time (migrations add/update) so this project
// doesn't need the full Api host (RabbitMQ, etc.) to spin up. Never used at runtime.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DsaPracticeDbContext>
{
    public DsaPracticeDbContext CreateDbContext(string[] args)
    {
        // Shares UserSecretsId with DsaPractice.Api, so `dotnet user-secrets set` run
        // against either project populates the connection string for both.
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<DesignTimeDbContextFactory>()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DsaPractice")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DsaPractice is not set. Run: dotnet user-secrets set \"ConnectionStrings:DsaPractice\" \"Host=localhost;Port=5432;Database=dsapractice;Username=dsapractice;Password=...\" --project source/DsaPractice.Api");

        var optionsBuilder = new DbContextOptionsBuilder<DsaPracticeDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            b => b.MigrationsAssembly(typeof(DesignTimeDbContextFactory).Assembly.FullName));

        return new DsaPracticeDbContext(optionsBuilder.Options);
    }
}
