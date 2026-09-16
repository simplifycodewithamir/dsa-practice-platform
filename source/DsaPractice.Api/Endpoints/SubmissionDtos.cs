using DsaPractice.DataAccess.Enums;
using DsaPractice.Api.Configuration;
using DsaPractice.DataAccess.Entities;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace DsaPractice.Api.Endpoints;

// No UserId: who is submitting is decided by the caller's identity, never by a field the caller
// can set (decision D3).
internal sealed record CreateSubmissionRequest(Guid QuestionId, string Language, string SourceCode);

internal sealed record SubmissionResponse(
    Guid Id,
    Guid QuestionId,
    Guid UserId,
    string Language,
    SubmissionStatus Status,
    SubmissionVerdict? Verdict,
    DateTimeOffset SubmittedAtUtc,
    DateTimeOffset? CompletedAtUtc = null,
    string? CompileOutput = null,
    IReadOnlyList<SubmissionTestResultResponse>? TestResults = null)
{
    public static SubmissionResponse FromEntity(Submission submission) => new(
        submission.Id,
        submission.QuestionId,
        submission.OwnerUserId,
        submission.Language,
        submission.Status,
        submission.Verdict,
        submission.SubmittedAtUtc,
        submission.CompletedAtUtc,
        submission.CompileOutput,
        TestResults: []);
}

/// <summary>
/// One test case's outcome. <see cref="ActualOutput"/> and <see cref="ErrorMessage"/> are null for
/// a hidden test case: the user learns that it failed, never what it contained -- otherwise the
/// hidden tests could be reconstructed one submission at a time.
/// </summary>
internal sealed record SubmissionTestResultResponse(
    int Ordinal,
    bool IsHidden,
    bool Passed,
    long ExecutionTimeMs,
    string? ActualOutput,
    string? ErrorMessage);

internal sealed class CreateSubmissionRequestValidator : AbstractValidator<CreateSubmissionRequest>
{
    // Default set (v1 scope — see dsa-practice-platform skill: C# and Python only) lives in
    // appsettings.json under "Submissions:SupportedLanguages", not hardcoded here.
    public CreateSubmissionRequestValidator(IOptionsSnapshot<SubmissionsOptions> options)
    {
        var supportedLanguages = options.Value.SupportedLanguages;

        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.SourceCode).NotEmpty();
        RuleFor(x => x.Language)
            .Must(language => supportedLanguages.Contains(language))
            .WithMessage($"Language must be one of: {string.Join(", ", supportedLanguages)}.");
    }
}
