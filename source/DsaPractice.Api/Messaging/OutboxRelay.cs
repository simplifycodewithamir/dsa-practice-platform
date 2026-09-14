using Microsoft.Extensions.Options;

namespace DsaPractice.Api.Messaging;

/// <summary>
/// Polls the outbox and publishes what it finds. Polling (rather than Postgres LISTEN/NOTIFY or an
/// in-process signal) is deliberate: it is the part that keeps working after a crash, a broker
/// outage, or a message written by something that never told anyone about it.
/// </summary>
internal sealed class OutboxRelay(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxRelay> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var outbox = options.Value;
        if (!outbox.RelayEnabled)
        {
            logger.LogInformation("Outbox relay is disabled by configuration.");
            return;
        }

        logger.LogInformation("Outbox relay started; polling every {PollInterval}.", outbox.PollInterval);
        using var timer = new PeriodicTimer(outbox.PollInterval);
        var sinceLastPurge = TimeSpan.Zero;

        while (await SafeWaitAsync(timer, stoppingToken))
        {
            try
            {
                // A scope per pass: DbContext is scoped, and a BackgroundService has no ambient
                // request scope to borrow one from.
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();

                await processor.ProcessPendingAsync(stoppingToken);

                sinceLastPurge += outbox.PollInterval;
                if (sinceLastPurge >= TimeSpan.FromHours(1))
                {
                    sinceLastPurge = TimeSpan.Zero;
                    var purged = await processor.PurgeProcessedAsync(stoppingToken);
                    if (purged > 0)
                    {
                        logger.LogInformation("Purged {Count} processed outbox messages.", purged);
                    }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Never let one bad pass kill the relay -- the next tick tries again.
                logger.LogError(exception, "Outbox relay pass failed; continuing.");
            }
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false; // shutting down
        }
    }
}
