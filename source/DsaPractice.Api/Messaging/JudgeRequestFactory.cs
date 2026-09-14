using DsaPractice.Contracts;
using DsaPractice.DataAccess.Entities;

namespace DsaPractice.Api.Messaging;

/// <summary>
/// Builds the self-contained judge request (decision D7). Lives in the Api, not in Contracts:
/// the message shape is shared with the Judge, the entities behind it are not.
/// </summary>
internal static class JudgeRequestFactory
{
    public static SubmissionJudgeRequested Create(Submission submission, Question question) => new(
        submission.Id,
        question.Id,
        submission.Language,
        submission.SourceCode,
        question.TimeLimitMs,
        question.MemoryLimitMb,
        [.. question.TestCases
            .OrderBy(tc => tc.Ordinal)
            // Hidden cases are withheld from the read API, never from the Judge.
            .Select(tc => new JudgeTestCase(tc.Id, tc.Ordinal, tc.Input, tc.ExpectedOutput))]);
}
