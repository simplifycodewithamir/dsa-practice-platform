using DsaPractice.ContentSeeding;
using DsaPractice.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// One-shot: brings a database up to date -- schema first (EF migrations), then content
// (content/questions/**, upserted by slug). Runs as the `migrator` service in docker-compose
// before the Api starts, and is safe to re-run: both halves are idempotent.
var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<Program>(optional: true);

// EF logs every statement at Information, which buries the one line that matters (what the run
// actually changed) under the INSERTs for every test case.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

var connectionString = builder.Configuration.GetConnectionString("DsaPractice");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "ConnectionStrings:DsaPractice is not set. Pass it as the ConnectionStrings__DsaPractice " +
        "environment variable, or set it with dotnet user-secrets for a local run.");
    return 1;
}

builder.Services.AddDbContext<DsaPracticeDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsAssembly(typeof(Program).Assembly.FullName)));
builder.Services.AddScoped<QuestionSeeder>();

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

// Ctrl+C / `docker stop` should abandon the run rather than leave it half-finished.
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

var contentPath = builder.Configuration["Content:Path"] ?? "content";

try
{
    using var scope = host.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();

    var pending = (await db.Database.GetPendingMigrationsAsync(cancellation.Token)).ToList();
    logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
        pending.Count, pending.Count == 0 ? "none" : string.Join(", ", pending));
    await db.Database.MigrateAsync(cancellation.Token);

    logger.LogInformation("Loading question content from {ContentPath}", Path.GetFullPath(contentPath));
    var content = ContentLoader.Load(contentPath);

    var seeder = scope.ServiceProvider.GetRequiredService<QuestionSeeder>();
    await seeder.SeedAsync(content, cancellation.Token);

    return 0;
}
catch (ContentException exception)
{
    // Authoring mistake, not an outage: the message lists every broken question.
    logger.LogError("{Message}", exception.Message);
    return 1;
}
catch (OperationCanceledException)
{
    logger.LogWarning("Cancelled before completing; nothing was left half-applied.");
    return 1;
}
