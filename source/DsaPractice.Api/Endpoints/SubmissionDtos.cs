using DsaPractice.Api.Configuration;
using DsaPractice.Api.DataAccess.Entities;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace DsaPractice.Api.Endpoints;

internal sealed record CreateSubmissionRequest(Guid QuestionId, string UserId, string Language, string SourceCode);

internal sealed record SubmissionResponse(
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

internal sealed class CreateSubmissionRequestValidator : AbstractValidator<CreateSubmissionRequest>
{
    // Default set (v1 scope — see dsa-practice-platform skill: C# and Python only) lives in
    // appsettings.json under "Submissions:SupportedLanguages", not hardcoded here.
    public CreateSubmissionRequestValidator(IOptionsSnapshot<SubmissionsOptions> options)
    {
        var supportedLanguages = options.Value.SupportedLanguages;

        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.SourceCode).NotEmpty();
        RuleFor(x => x.Language)
            .Must(language => supportedLanguages.Contains(language))
            .WithMessage($"Language must be one of: {string.Join(", ", supportedLanguages)}.");
    }
}
