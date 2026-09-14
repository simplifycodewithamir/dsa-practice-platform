using DsaPractice.Contracts;
using DsaPractice.Judge.Execution;

namespace DsaPractice.Judge.IntegrationTests;

/// <summary>
/// Stands in for the sandbox so a test can choose what "running" a submission produces, including
/// blowing up. Not a mocking framework: the consumer calls it from the broker's threads, so a
/// plain object with a swappable behaviour is easier to reason about than a strict mock.
/// </summary>
public sealed class StubSandboxExecutor : ISandboxExecutor
{
    private Func<SubmissionJudgeRequested, ExecutionOutcome> _behaviour = AllPassed;

    public int ExecutionCount { get; private set; }

    public void Returns(Func<SubmissionJudgeRequested, ExecutionOutcome> behaviour) => _behaviour = behaviour;

    public void Throws(Exception exception) => _behaviour = _ => throw exception;

    public void Reset()
    {
        _behaviour = AllPassed;
        ExecutionCount = 0;
    }

    public Task<ExecutionOutcome> ExecuteAsync(SubmissionJudgeRequested request, CancellationToken cancellationToken)
    {
        ExecutionCount++;
        return Task.FromResult(_behaviour(request));
    }

    private static ExecutionOutcome AllPassed(SubmissionJudgeRequested request) => new(
        [.. request.TestCases.Select(tc => new TestCaseOutcome(tc.TestCaseId, tc.Ordinal, TestCaseStatus.Passed, tc.ExpectedOutput, null, 1))]);
}
