using System.Text;
using DsaPractice.DataAccess;
using DsaPractice.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DsaPractice.Api.Messaging;

/// <summary>
/// One pass of the outbox relay. Separate from the <see cref="OutboxRelay"/> background loop so it
/// can be driven directly -- by a test, or later by anything that wants to flush on demand.
/// </summary>
internal sealed class OutboxProcessor(
    DsaPracticeDbContext db,
    IMessagePublisher publisher,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger)
{
    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken)
    {
        var outbox = options.Value;
        var now = timeProvider.GetUtcNow();

        // FOR UPDATE SKIP LOCKED: rows claimed by this pass are locked for its duration, and a
        // second relay (another instance, or a future one) skips them instead of blocking or
        // double-publishing. The transaction stays open across the publish, which is what lets a
        // failed publish roll back to "still pending" -- fine at this scale, and the reason the
        // batch is small.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var pending = await db.OutboxMessages
            .FromSql($"""
                SELECT * FROM "OutboxMessages"
                WHERE "ProcessedAtUtc" IS NULL AND "NextAttemptAtUtc" <= {now}
                ORDER BY "OccurredAtUtc"
                LIMIT {outbox.BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        var published = 0;

        foreach (var message in pending)
        {
            try
            {
                await publisher.PublishAsync(
                    message.RoutingKey,
                    message.Type,
                    message.MessageId,
                    Encoding.UTF8.GetBytes(message.Payload),
                    cancellationToken);

                message.ProcessedAtUtc = timeProvider.GetUtcNow();
                message.LastError = null;
                published++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Publishing is at-least-once: if the broker confirmed but this crashes before the
                // commit below, the message is published again on the next pass. Consumers have to
                // be idempotent (item 9).
                Backoff(message, exception, outbox);

                logger.LogError(
                    exception,
                    "Publishing outbox message {MessageId} ({Type}) failed on attempt {AttemptCount}; retrying at {NextAttemptAtUtc}",
                    message.MessageId, message.Type, message.AttemptCount, message.NextAttemptAtUtc);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return published;
    }

    private void Backoff(OutboxMessage message, Exception exception, OutboxOptions outbox)
    {
        message.AttemptCount++;
        message.LastError = Truncate(exception.Message, 2000);

        // Exponential, capped: a broker that is down for an hour shouldn't be hammered once a
        // second for an hour.
        var delayTicks = outbox.BaseRetryDelay.Ticks * (long)Math.Pow(2, Math.Min(message.AttemptCount - 1, 20));
        var delay = TimeSpan.FromTicks(Math.Min(delayTicks, outbox.MaxRetryDelay.Ticks));

        message.NextAttemptAtUtc = timeProvider.GetUtcNow().Add(delay);
    }

    /// <summary>Deletes long-processed rows so the table doesn't grow without bound.</summary>
    public async Task<int> PurgeProcessedAsync(CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow() - options.Value.Retention;

        return await db.OutboxMessages
            .Where(m => m.ProcessedAtUtc != null && m.ProcessedAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
