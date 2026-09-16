using DsaPractice.Contracts;
using Microsoft.Extensions.Logging;

namespace DsaPractice.Judge.Execution;

/// <summary>
/// Stand-in until item 11 builds the Docker sandbox. It executes nothing: every test case is
/// reported as passed. It exists so the whole loop -- submit, queue, judge, publish, store, poll --
/// can be closed and tested before any submitted code is ever run.
///
/// Registered only when Judge:UseFakeExecutor is true, which is the default until item 11 and
/// which the Judge logs loudly at startup, so this can never be mistaken for real judging.
/// </summary>
public sealed class FakeSandboxExecutor(ILogger<FakeSandboxExecutor> logger) : ISandboxExecutor
{
    public Task<ExecutionOutcome> ExecuteAsync(SubmissionJudgeRequested request, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "FAKE executor: reporting {TestCaseCount} test case(s) as passed for submission {SubmissionId} without running anything.",
            request.TestCases.Count, request.SubmissionId);

        var outcomes = request.TestCases
            .OrderBy(tc => tc.Ordinal)
            .Select(tc => new TestCaseOutcome(
                tc.TestCaseId,
                tc.Ordinal,
                TestCaseStatus.Passed,
                ActualOutput: tc.ExpectedOutput,
                ErrorMessage: null,
                ExecutionTimeMs: 0))
            .ToList();

        return Task.FromResult(new ExecutionOutcome(outcomes));
    }
}
