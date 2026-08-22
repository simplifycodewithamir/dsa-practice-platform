using DsaPractice.Api.DataAccess;
using DsaPractice.Api.DataAccess.Entities;
using DsaPractice.Api.Endpoints;
using DsaPractice.Api.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace DsaPractice.Api.Services;

internal interface ISubmissionsService
{
    Task<SubmissionResponse> CreateSubmissionAsync(CreateSubmissionRequest request, CancellationToken cancellationToken);

    Task<SubmissionResponse> GetSubmissionByIdAsync(Guid id, CancellationToken cancellationToken);
}

internal sealed class SubmissionsService(DsaPracticeDbContext db, TimeProvider timeProvider) : ISubmissionsService
{
    public async Task<SubmissionResponse> CreateSubmissionAsync(CreateSubmissionRequest request, CancellationToken cancellationToken)
    {
        // Business rule: a submission can only be created against a question that actually exists.
        var questionExists = await db.Questions
            .AsNoTracking()
            .AnyAsync(q => q.Id == request.QuestionId, cancellationToken);

        if (!questionExists)
        {
            throw new NotFoundException($"Question '{request.QuestionId}' was not found.");
        }

        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            QuestionId = request.QuestionId,
            UserId = request.UserId,
            Language = request.Language,
            SourceCode = request.SourceCode,
            Status = "Pending",
            SubmittedAtUtc = timeProvider.GetUtcNow()
        };

        db.Submissions.Add(submission);
        await db.SaveChangesAsync(cancellationToken);

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
