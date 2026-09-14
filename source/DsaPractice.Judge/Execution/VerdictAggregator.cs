using DsaPractice.Contracts;

namespace DsaPractice.Judge.Execution;

/// <summary>
/// Turns per-test outcomes into the single verdict the user sees. The rule is the usual one: the
/// first failing test case decides, in run order, so "Wrong Answer on test 3" is reproducible and
/// a later, worse-looking failure doesn't mask an earlier one.
/// </summary>
public static class VerdictAggregator
{
    public static SubmissionJudged Aggregate(Guid submissionId, ExecutionOutcome outcome)
    {
        var results = outcome.TestCases
            .OrderBy(tc => tc.Ordinal)
            .Select(tc => new TestCaseResult(
                tc.TestCaseId,
                tc.Ordinal,
                tc.Status == TestCaseStatus.Passed,
                tc.ActualOutput,
                tc.ErrorMessage,
                tc.ExecutionTimeMs))
            .ToList();

        if (outcome.CompilationFailed)
        {
            // Nothing ran, so there are no per-test results to report.
            return new SubmissionJudged(submissionId, JudgeVerdict.CompilationError, [], outcome.CompileOutput);
        }

        var firstFailure = outcome.TestCases
            .OrderBy(tc => tc.Ordinal)
            .FirstOrDefault(tc => tc.Status != TestCaseStatus.Passed);

        var verdict = firstFailure is null
            ? JudgeVerdict.Accepted
            : firstFailure.Status switch
            {
                TestCaseStatus.WrongAnswer => JudgeVerdict.WrongAnswer,
                TestCaseStatus.TimeLimitExceeded => JudgeVerdict.TimeLimitExceeded,
                TestCaseStatus.MemoryLimitExceeded => JudgeVerdict.MemoryLimitExceeded,
                TestCaseStatus.RuntimeError => JudgeVerdict.RuntimeError,
                _ => JudgeVerdict.InternalError
            };

        return new SubmissionJudged(submissionId, verdict, results, outcome.CompileOutput);
    }
}
