using System.Text.Json;
using DsaPractice.Contracts;
using DsaPractice.DataAccess;
using DsaPractice.DataAccess.Entities;
using Microsoft.Extensions.Options;

namespace DsaPractice.Api.Messaging;

internal interface IOutboxWriter
{
    /// <summary>
    /// Stages a judge request on the caller's <see cref="DsaPracticeDbContext"/>. It is committed by
    /// the caller's own SaveChanges, in the same transaction as the submission -- that is the whole
    /// point: either both exist or neither does.
    /// </summary>
    void Enqueue(DsaPracticeDbContext db, SubmissionJudgeRequested message);
}

internal sealed class OutboxWriter(IOptions<RabbitMqOptions> options, TimeProvider timeProvider) : IOutboxWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public void Enqueue(DsaPracticeDbContext db, SubmissionJudgeRequested message)
    {
        var now = timeProvider.GetUtcNow();

        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = nameof(SubmissionJudgeRequested),
            RoutingKey = options.Value.JudgeRequestedRoutingKey,
            MessageId = message.SubmissionId.ToString(),
            Payload = JsonSerializer.Serialize(message, SerializerOptions),
            OccurredAtUtc = now,
            NextAttemptAtUtc = now // due immediately
        });
    }
}
