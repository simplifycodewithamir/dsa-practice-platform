using DsaPractice.Contracts;
using DsaPractice.Judge.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DsaPractice.Judge.UnitTests.Execution;

/// <summary>
/// The stand-in executor passes everything without running it. That is only safe while it is
/// impossible to mistake for real judging, so what is pinned here is that it reports a full,
/// correctly-shaped result and that it never touches the submitted code.
/// </summary>
public class FakeSandboxExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_ReportsEveryTestCaseAsPassed()
    {
        var request = NewRequest(3);

        var outcome = await Execute(request);

        Assert.Equal(3, outcome.TestCases.Count);
        Assert.All(outcome.TestCases, tc => Assert.Equal(TestCaseStatus.Passed, tc.Status));
    }

    [Fact]
    public async Task ExecuteAsync_EchoesTheExpectedOutputBecauseNothingRan()
    {
        var request = NewRequest(1);

        var outcome = await Execute(request);

        var result = Assert.Single(outcome.TestCases);
        Assert.Equal(request.TestCases[0].ExpectedOutput, result.ActualOutput);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(0, result.ExecutionTimeMs); // no run, so no honest timing to report
    }

    [Fact]
    public async Task ExecuteAsync_OrdersResultsByOrdinal()
    {
        var request = NewRequest(3) with
        {
            TestCases = [.. NewRequest(3).TestCases.OrderByDescending(tc => tc.Ordinal)]
        };

        var outcome = await Execute(request);

        Assert.Equal([1, 2, 3], outcome.TestCases.Select(tc => tc.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_CarriesTestCaseIdsBackSoResultsCanBeMatched()
    {
        var request = NewRequest(2);

        var outcome = await Execute(request);

        Assert.Equal(
            request.TestCases.Select(tc => tc.TestCaseId),
            outcome.TestCases.Select(tc => tc.TestCaseId));
    }

    [Fact]
    public async Task ExecuteAsync_NeverReportsACompilationFailure()
    {
        // Nothing is compiled, so a compile verdict from here would be a lie the Api would store.
        var outcome = await Execute(NewRequest(1));

        Assert.False(outcome.CompilationFailed);
        Assert.Null(outcome.CompileOutput);
    }

    [Fact]
    public async Task ExecuteAsync_AggregatesToAccepted()
    {
        // What the Judge actually publishes: the fake's whole purpose is closing the loop with an
        // Accepted verdict, and the aggregator is what turns its outcome into one.
        var request = NewRequest(2);

        var judged = VerdictAggregator.Aggregate(request.SubmissionId, await Execute(request));

        Assert.Equal(JudgeVerdict.Accepted, judged.Verdict);
    }

    private static Task<ExecutionOutcome> Execute(SubmissionJudgeRequested request) =>
        new FakeSandboxExecutor(NullLogger<FakeSandboxExecutor>.Instance)
            .ExecuteAsync(request, CancellationToken.None);

    private static SubmissionJudgeRequested NewRequest(int testCaseCount) => new(
        SubmissionId: Guid.NewGuid(),
        QuestionId: Guid.NewGuid(),
        Language: "python",
        SourceCode: "print(1)",
        TimeLimitMs: 1000,
        MemoryLimitMb: 128,
        TestCases: [.. Enumerable.Range(1, testCaseCount)
            .Select(i => new JudgeTestCase(Guid.NewGuid(), i, $"in-{i}", $"out-{i}"))]);
}
