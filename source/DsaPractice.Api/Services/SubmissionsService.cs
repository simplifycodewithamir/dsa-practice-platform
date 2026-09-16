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
    IOutboxWriter outboxWriter) : ISubmissionsService
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

        // The judge request is staged on the same DbContext, so this one SaveChanges commits the
        // submission and the message to publish in a single transaction. Nothing here talks to the
        // broker: a broker outage can no longer fail a submission, and a crash can no longer leave
        // a saved submission with nothing queued to judge it. The relay publishes it, at least
        // once, whenever the broker is reachable again.
        outboxWriter.Enqueue(db, JudgeRequestFactory.Create(submission, question));

        await db.SaveChangesAsync(cancellationToken);

        return SubmissionResponse.FromEntity(submission);
    }

    public async Task<SubmissionResponse> GetSubmissionByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var submission = await db.Submissions
            .AsNoTracking()
            .Include(s => s.TestResults)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Submission '{id}' was not found.");

        // Which test cases are hidden lives on the question, not on the result, so the two are
        // joined here rather than duplicating IsHidden onto every result row.
        var hiddenTestCaseIds = await db.TestCases
            .AsNoTracking()
            .Where(tc => tc.QuestionId == submission.QuestionId && tc.IsHidden)
            .Select(tc => tc.Id)
            .ToListAsync(cancellationToken);

        var results = submission.TestResults
            .OrderBy(r => r.Ordinal)
            .Select(r =>
            {
                var isHidden = hiddenTestCaseIds.Contains(r.TestCaseId);
                return new SubmissionTestResultResponse(
                    r.Ordinal,
                    isHidden,
                    r.Passed,
                    r.ExecutionTimeMs,
                    isHidden ? null : r.ActualOutput,
                    isHidden ? null : r.ErrorMessage);
            })
            .ToList();

        return SubmissionResponse.FromEntity(submission) with { TestResults = results };
    }
}
