using DsaPractice.Contracts;

namespace DsaPractice.Judge.Execution;

/// <summary>
/// Runs one submission against its test cases. The only place submitted code is ever executed --
/// item 11 implements this with an ephemeral, resource-capped Docker container per run.
/// </summary>
public interface ISandboxExecutor
{
    Task<ExecutionOutcome> ExecuteAsync(SubmissionJudgeRequested request, CancellationToken cancellationToken);
}

/// <summary>
/// What running the submission produced. The verdict is derived from this by
/// <see cref="VerdictAggregator"/> rather than decided by the executor, so the rule lives in one
/// testable place regardless of which executor ran.
/// </summary>
public sealed record ExecutionOutcome(
    IReadOnlyList<TestCaseOutcome> TestCases,
    string? CompileOutput = null,
    bool CompilationFailed = false);

public sealed record TestCaseOutcome(
    Guid TestCaseId,
    int Ordinal,
    TestCaseStatus Status,
    string? ActualOutput,
    string? ErrorMessage,
    long ExecutionTimeMs);

public enum TestCaseStatus
{
    Passed,
    WrongAnswer,
    TimeLimitExceeded,
    MemoryLimitExceeded,
    RuntimeError
}
