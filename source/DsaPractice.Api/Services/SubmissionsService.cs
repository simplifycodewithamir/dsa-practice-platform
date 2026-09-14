using DsaPractice.DataAccess.Enums;
using DsaPractice.DataAccess;
using DsaPractice.DataAccess.Entities;
using DsaPractice.Api.Endpoints;
using DsaPractice.Api.Exceptions;
using DsaPractice.Api.Messaging;
using Microsoft.EntityFrameworkCore;

namespace DsaPractice.Api.Services;

internal interface ISubmissionsService
{
    Task<SubmissionResponse> CreateSubmissionAsync(CreateSubmissionRequest request, CancellationToken cancellationToken);

    Task<SubmissionResponse> GetSubmissionByIdAsync(Guid id, CancellationToken cancellationToken);
}

internal sealed class SubmissionsService(
    DsaPracticeDbContext db,
    TimeProvider timeProvider,
    IJudgeRequestPublisher judgeRequestPublisher) : ISubmissionsService
{
    public async Task<SubmissionResponse> CreateSubmissionAsync(CreateSubmissionRequest request, CancellationToken cancellationToken)
    {
        // Business rule: a submission can only be created against a question that actually exists.
        // Test cases come along because the judge request carries them (decision D7).
        var question = await db.Questions
            .AsNoTracking()
            .Include(q => q.TestCases)
            .FirstOrDefaultAsync(q => q.Id == request.QuestionId, cancellationToken)
            ?? throw new NotFoundException($"Question '{request.QuestionId}' was not found.");

        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            QuestionId = request.QuestionId,
            UserId = request.UserId,
            Language = request.Language,
            SourceCode = request.SourceCode,
            Status = SubmissionStatus.Pending,
            SubmittedAtUtc = timeProvider.GetUtcNow()
        };

        db.Submissions.Add(submission);
        await db.SaveChangesAsync(cancellationToken);

        // Deliberately naive: the row is committed, then the message is published, as two separate
        // operations. If the process dies in between -- or the broker is unreachable -- the
        // submission sits Pending forever with nothing queued to judge it, and the caller sees a
        // 500 for a submission that was in fact saved. That gap is the dual-write problem, and
        // item 8 closes it with a transactional outbox. Left visible on purpose.
        await judgeRequestPublisher.PublishAsync(JudgeRequestFactory.Create(submission, question), cancellationToken);

        return SubmissionResponse.FromEntity(submission);
    }

    public async Task<SubmissionResponse> GetSubmissionByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var submission = await db.Submissions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Submission '{id}' was not found.");

        return SubmissionResponse.FromEntity(submission);
    }
}
