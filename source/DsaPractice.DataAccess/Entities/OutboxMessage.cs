namespace DsaPractice.DataAccess.Entities;

/// <summary>
/// A message to publish, written in the same transaction as the state change that produced it.
/// The relay reads this table and publishes to the broker, which is what makes "saved" and
/// "queued for judging" a single atomic outcome instead of two operations that can disagree.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    /// <summary>Contract type name, e.g. "SubmissionJudgeRequested" -- carried on the AMQP message.</summary>
    public required string Type { get; set; }

    /// <summary>Where to publish it. Stored, so the relay needs no knowledge of message types.</summary>
    public required string RoutingKey { get; set; }

    /// <summary>Broker message id; the business key of whatever produced this (the submission id).</summary>
    public required string MessageId { get; set; }

    /// <summary>Serialized contract, published as-is.</summary>
    public required string Payload { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    /// <summary>Null until the broker has confirmed it. The relay only looks at null rows.</summary>
    public DateTimeOffset? ProcessedAtUtc { get; set; }

    /// <summary>When the relay may next try. Backs off after a failure instead of spinning.</summary>
    public DateTimeOffset NextAttemptAtUtc { get; set; }

    public int AttemptCount { get; set; }

    /// <summary>Why the last attempt failed, for diagnosing a row that keeps retrying.</summary>
    public string? LastError { get; set; }
}
