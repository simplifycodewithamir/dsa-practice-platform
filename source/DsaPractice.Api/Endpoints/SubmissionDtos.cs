using DsaPractice.Api.DataAccess.Entities;
using FluentValidation;

namespace DsaPractice.Api.Endpoints;

public sealed record CreateSubmissionRequest(Guid QuestionId, string UserId, string Language, string SourceCode);

public sealed record SubmissionResponse(
    Guid Id,
    Guid QuestionId,
    string UserId,
    string Language,
    string Status,
    DateTimeOffset SubmittedAtUtc)
{
    public static SubmissionResponse FromEntity(Submission submission) => new(
        submission.Id,
        submission.QuestionId,
        submission.UserId,
        submission.Language,
        submission.Status,
        submission.SubmittedAtUtc);
}

public sealed class CreateSubmissionRequestValidator : AbstractValidator<CreateSubmissionRequest>
{
    // v1 scope — see dsa-practice-platform skill: C# and Python only.
    public static readonly string[] SupportedLanguages = ["csharp", "python"];

    public CreateSubmissionRequestValidator()
    {
        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.SourceCode).NotEmpty();
        RuleFor(x => x.Language)
            .Must(language => SupportedLanguages.Contains(language))
            .WithMessage($"Language must be one of: {string.Join(", ", SupportedLanguages)}.");
    }
}
