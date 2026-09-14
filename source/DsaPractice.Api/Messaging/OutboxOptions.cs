namespace DsaPractice.Api.Messaging;

internal sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>How often the relay looks for pending messages.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Rows claimed per pass.</summary>
    public int BatchSize { get; init; } = 20;

    /// <summary>First retry delay after a failed publish; doubles per attempt up to <see cref="MaxRetryDelay"/>.</summary>
    public TimeSpan BaseRetryDelay { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Processed rows older than this are deleted, so the table doesn't grow forever.</summary>
    public TimeSpan Retention { get; init; } = TimeSpan.FromDays(7);

    /// <summary>Turned off in tests that drive the processor directly.</summary>
    public bool RelayEnabled { get; init; } = true;
}
