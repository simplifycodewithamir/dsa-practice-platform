namespace DsaPractice.Api.Messaging;

/// <summary>
/// Broker connection and topology. The URI carries credentials, so it comes from user-secrets
/// locally and the environment in a deployment -- never from appsettings.json.
/// </summary>
internal sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public required string Uri { get; init; }

    /// <summary>Direct exchange everything submission-related is published to.</summary>
    public string Exchange { get; init; } = "dsa.submissions";

    /// <summary>Routing key and queue name for judge requests -- the Judge consumes this queue.</summary>
    public string JudgeRequestedRoutingKey { get; init; } = "submission.judge-requested";

    public string JudgeRequestedQueue { get; init; } = "submission.judge-requested";
}
