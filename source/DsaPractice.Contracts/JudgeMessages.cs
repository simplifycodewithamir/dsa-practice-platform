namespace DsaPractice.Contracts;

// Published by Api -> RabbitMQ ("submission.judge-requested"), consumed by Judge.
//
// Self-contained on purpose (decision D7): the Judge never reads the Api's database, so everything
// needed to run and mark a submission travels in the message -- the code, the language, the limits
// and every test case, hidden ones included.
public sealed record SubmissionJudgeRequested(
    Guid SubmissionId,
    Guid QuestionId,
    string Language,
    string SourceCode,
    int TimeLimitMs,
    int MemoryLimitMb,
    IReadOnlyList<JudgeTestCase> TestCases);

/// <summary>
/// One test case to run. <see cref="TestCaseId"/> comes back on the result so the Api can store a
/// per-test-case outcome; <see cref="Ordinal"/> is the run order and what the user sees.
/// </summary>
public sealed record JudgeTestCase(
    Guid TestCaseId,
    int Ordinal,
    string Input,
    string ExpectedOutput);

// Published by Judge -> RabbitMQ ("submission.judged"), consumed by Api.
public sealed record SubmissionJudged(
    Guid SubmissionId,
    JudgeVerdict Verdict,
    IReadOnlyList<TestCaseResult> TestCaseResults,
    string? CompileOutput = null);

/// <summary>
/// Outcome of a judged submission. Mirrors the Api's SubmissionVerdict by name rather than sharing
/// the type: the Judge must not reference the Api's data access, and a wire contract that changes
/// only when the contract changes is the point of a separate assembly.
/// </summary>
public enum JudgeVerdict
{
    Accepted,
    WrongAnswer,
    TimeLimitExceeded,
    MemoryLimitExceeded,
    RuntimeError,
    CompilationError,
    InternalError
}

public sealed record TestCaseResult(
    Guid TestCaseId,
    int Ordinal,
    bool Passed,
    string? ActualOutput,
    string? ErrorMessage,
    long ExecutionTimeMs);
