using DsaPractice.Api.DataAccess;
using DsaPractice.Api.DataAccess.Entities;
using DsaPractice.Api.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DsaPractice.Api.Endpoints;

public static class SubmissionsEndpoints
{
    public static RouteGroupBuilder MapSubmissionsEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreateSubmission);
        group.MapGet("/{id:guid}", GetSubmissionById);

        return group;
    }

    private static async Task<IResult> CreateSubmission(
        CreateSubmissionRequest request,
        IValidator<CreateSubmissionRequest> validator,
        DsaPracticeDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new BadRequestException(
                "Submission request failed validation.",
                validationResult.Errors.Select(e => new { field = e.PropertyName, error = e.ErrorMessage }));
        }

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

        return Results.Created($"/api/v1/submissions/{submission.Id}", SubmissionResponse.FromEntity(submission));
    }

    private static async Task<IResult> GetSubmissionById(Guid id, DsaPracticeDbContext db, CancellationToken cancellationToken)
    {
        var submission = await db.Submissions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Submission '{id}' was not found.");

        return Results.Ok(SubmissionResponse.FromEntity(submission));
    }
}
