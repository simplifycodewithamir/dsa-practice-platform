namespace DsaPractice.Contracts;

// Published by Api -> RabbitMQ ("submission.judge-requested"), consumed by Judge
public sealed record SubmissionJudgeRequested(
    Guid SubmissionId,
    Guid QuestionId,
    string Language,
    string SourceCode);

// Published by Judge -> RabbitMQ ("submission.judged"), consumed by Api
public sealed record SubmissionJudged(
    Guid SubmissionId,
    string Verdict, // Passed | Failed | Error | TimeLimitExceeded | MemoryLimitExceeded
    IReadOnlyList<TestCaseResult> TestCaseResults);

public sealed record TestCaseResult(
    Guid TestCaseId,
    bool Passed,
    string? ActualOutput,
    string? ErrorMessage,
    long ExecutionTimeMs);
