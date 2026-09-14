using DsaPractice.Contracts;
using DsaPractice.DataAccess;
using DsaPractice.DataAccess.Entities;
using DsaPractice.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;

namespace DsaPractice.Api.Messaging;

/// <summary>
/// Applies a judged result to the submission it belongs to.
///
/// Idempotent by design, not by luck: the outbox publishes at least once and the Judge acks after
/// publishing, so the same result can arrive twice. A submission that is already Completed is left
/// exactly as it is.
/// </summary>
internal sealed class JudgedResultRecorder(DsaPracticeDbContext db, TimeProvider timeProvider, ILogger<JudgedResultRecorder> logger)
{
    public async Task<RecordOutcome> RecordAsync(SubmissionJudged result, CancellationToken cancellationToken)
    {
        var submission = await db.Submissions
            .Include(s => s.TestResults)
            .FirstOrDefaultAsync(s => s.Id == result.SubmissionId, cancellationToken);

        if (submission is null)
        {
            // Nothing to apply it to, and nothing retrying will fix.
            logger.LogWarning("Judged result for unknown submission {SubmissionId}; discarding it.", result.SubmissionId);
            return RecordOutcome.UnknownSubmission;
        }

        if (submission.Status == SubmissionStatus.Completed)
        {
            logger.LogInformation(
                "Submission {SubmissionId} is already {Verdict}; ignoring a duplicate result.",
                submission.Id, submission.Verdict);
            return RecordOutcome.AlreadyRecorded;
        }

        submission.Status = SubmissionStatus.Completed;
        submission.Verdict = MapVerdict(result.Verdict);
        submission.CompletedAtUtc = timeProvider.GetUtcNow();
        submission.CompileOutput = Truncate(result.CompileOutput);

        submission.TestResults.Clear();
        foreach (var testResult in result.TestCaseResults.OrderBy(r => r.Ordinal))
        {
            submission.TestResults.Add(new SubmissionTestResult
            {
                SubmissionId = submission.Id,
                TestCaseId = testResult.TestCaseId,
                Ordinal = testResult.Ordinal,
                Passed = testResult.Passed,
                ActualOutput = Truncate(testResult.ActualOutput),
                ErrorMessage = Truncate(testResult.ErrorMessage),
                ExecutionTimeMs = testResult.ExecutionTimeMs
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Submission {SubmissionId} recorded as {Verdict} with {ResultCount} test results.",
            submission.Id, submission.Verdict, submission.TestResults.Count);

        return RecordOutcome.Recorded;
    }

    /// <summary>
    /// Wire verdict to stored verdict. Deliberately explicit rather than a name-based parse: the
    /// two enums are allowed to drift, and a new value on either side should be a compile error
    /// here, not a silent mismatch.
    /// </summary>
    private static SubmissionVerdict MapVerdict(JudgeVerdict verdict) => verdict switch
    {
        JudgeVerdict.Accepted => SubmissionVerdict.Accepted,
        JudgeVerdict.WrongAnswer => SubmissionVerdict.WrongAnswer,
        JudgeVerdict.TimeLimitExceeded => SubmissionVerdict.TimeLimitExceeded,
        JudgeVerdict.MemoryLimitExceeded => SubmissionVerdict.MemoryLimitExceeded,
        JudgeVerdict.RuntimeError => SubmissionVerdict.RuntimeError,
        JudgeVerdict.CompilationError => SubmissionVerdict.CompilationError,
        JudgeVerdict.InternalError => SubmissionVerdict.InternalError,
        _ => SubmissionVerdict.InternalError
    };

    private static string? Truncate(string? value) =>
        value is null || value.Length <= SubmissionTestResult.MaxOutputLength
            ? value
            : value[..SubmissionTestResult.MaxOutputLength];
}

internal enum RecordOutcome
{
    Recorded,
    AlreadyRecorded,
    UnknownSubmission
}
