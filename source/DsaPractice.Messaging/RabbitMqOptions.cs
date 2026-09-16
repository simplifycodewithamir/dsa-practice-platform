namespace DsaPractice.Messaging;

/// <summary>
/// Broker connection and topology, shared by every service that talks to RabbitMQ. The names live
/// in one place because both sides declare the same topology and RabbitMQ refuses a redeclaration
/// that disagrees with what already exists.
///
/// The URI carries credentials, so it comes from user-secrets locally and the environment in a
/// deployment -- never from appsettings.json.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public required string Uri { get; init; }

    /// <summary>Direct exchange everything submission-related is published to.</summary>
    public string Exchange { get; init; } = "dsa.submissions";

    /// <summary>Where messages go when a consumer rejects them; nothing publishes here directly.</summary>
    public string DeadLetterExchange { get; init; } = "dsa.submissions.dlx";

    /// <summary>Api -> Judge.</summary>
    public string JudgeRequestedRoutingKey { get; init; } = "submission.judge-requested";

    public string JudgeRequestedQueue { get; init; } = "submission.judge-requested";

    /// <summary>Judge -> Api.</summary>
    public string JudgedRoutingKey { get; init; } = "submission.judged";

    public string JudgedQueue { get; init; } = "submission.judged";

    /// <summary>Rejected messages are parked here for a human to look at.</summary>
    public string DeadLetterQueue { get; init; } = "submission.dead-letter";

    public string DeadLetterRoutingKey { get; init; } = "submission.dead-letter";
}
