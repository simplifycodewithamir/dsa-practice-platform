using DsaPractice.Contracts;
using DsaPractice.Judge.Execution;
using Xunit;

namespace DsaPractice.Judge.UnitTests.Execution;

public class VerdictAggregatorTests
{
    private static readonly Guid SubmissionId = Guid.NewGuid();

    [Fact]
    public void Aggregate_AllTestsPassed_IsAccepted()
    {
        var outcome = new ExecutionOutcome([Outcome(1, TestCaseStatus.Passed), Outcome(2, TestCaseStatus.Passed)]);

        var result = VerdictAggregator.Aggregate(SubmissionId, outcome);

        Assert.Equal(JudgeVerdict.Accepted, result.Verdict);
        Assert.All(result.TestCaseResults, r => Assert.True(r.Passed));
    }

    [Theory]
    [InlineData(TestCaseStatus.WrongAnswer, JudgeVerdict.WrongAnswer)]
    [InlineData(TestCaseStatus.TimeLimitExceeded, JudgeVerdict.TimeLimitExceeded)]
    [InlineData(TestCaseStatus.MemoryLimitExceeded, JudgeVerdict.MemoryLimitExceeded)]
    [InlineData(TestCaseStatus.RuntimeError, JudgeVerdict.RuntimeError)]
    public void Aggregate_SingleFailure_TakesThatFailuresVerdict(TestCaseStatus status, JudgeVerdict expected)
    {
        var outcome = new ExecutionOutcome([Outcome(1, TestCaseStatus.Passed), Outcome(2, status)]);

        Assert.Equal(expected, VerdictAggregator.Aggregate(SubmissionId, outcome).Verdict);
    }

    [Fact]
    public void Aggregate_SeveralFailures_TheFirstInRunOrderDecides()
    {
        // Out of order on purpose: ordinal 2 failed first, even though the list starts at 3.
        var outcome = new ExecutionOutcome(
        [
            Outcome(3, TestCaseStatus.RuntimeError),
            Outcome(1, TestCaseStatus.Passed),
            Outcome(2, TestCaseStatus.WrongAnswer)
        ]);

        var result = VerdictAggregator.Aggregate(SubmissionId, outcome);

        Assert.Equal(JudgeVerdict.WrongAnswer, result.Verdict);
        Assert.Equal([1, 2, 3], result.TestCaseResults.Select(r => r.Ordinal));
    }

    [Fact]
    public void Aggregate_CompilationFailed_ReportsCompilationErrorWithNoTestResults()
    {
        var outcome = new ExecutionOutcome([], CompileOutput: "error CS1002: ; expected", CompilationFailed: true);

        var result = VerdictAggregator.Aggregate(SubmissionId, outcome);

        Assert.Equal(JudgeVerdict.CompilationError, result.Verdict);
        Assert.Empty(result.TestCaseResults);   // nothing ran, so there is nothing to report per test
        Assert.Equal("error CS1002: ; expected", result.CompileOutput);
    }

    [Fact]
    public void Aggregate_CarriesTestCaseIdsAndTimingsBack()
    {
        var testCaseId = Guid.NewGuid();
        var outcome = new ExecutionOutcome(
            [new TestCaseOutcome(testCaseId, 1, TestCaseStatus.WrongAnswer, "4", "none", 42)]);

        var result = VerdictAggregator.Aggregate(SubmissionId, outcome);

        var single = Assert.Single(result.TestCaseResults);
        Assert.Equal(testCaseId, single.TestCaseId); // the Api matches results back by this
        Assert.False(single.Passed);
        Assert.Equal("4", single.ActualOutput);
        Assert.Equal(42, single.ExecutionTimeMs);
    }

    private static TestCaseOutcome Outcome(int ordinal, TestCaseStatus status) =>
        new(Guid.NewGuid(), ordinal, status, ActualOutput: "out", ErrorMessage: null, ExecutionTimeMs: 1);
}
