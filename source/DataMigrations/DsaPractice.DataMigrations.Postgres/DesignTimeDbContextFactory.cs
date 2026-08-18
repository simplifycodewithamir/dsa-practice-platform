using DsaPractice.Api.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DsaPractice.DataMigrations.Postgres;

// Used only by `dotnet ef` at design time (migrations add/update) so this project
// doesn't need the full Api host (RabbitMQ, etc.) to spin up. Never used at runtime.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DsaPracticeDbContext>
{
    public DsaPracticeDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DsaPracticeDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=dsapractice;Username=dsapractice;Password=localdevonly",
            b => b.MigrationsAssembly(typeof(DesignTimeDbContextFactory).Assembly.FullName));

        return new DsaPracticeDbContext(optionsBuilder.Options);
    }
}
