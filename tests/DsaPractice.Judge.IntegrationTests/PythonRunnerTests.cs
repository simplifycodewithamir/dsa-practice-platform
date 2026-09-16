using Docker.DotNet;
using DsaPractice.Contracts;
using DsaPractice.Judge.Execution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DsaPractice.Judge.IntegrationTests;

/// <summary>
/// Judges real submissions in the real Python runner, using the configuration the Judge actually
/// ships with (read from its appsettings.json, not a copy) and the reference solutions committed
/// next to the questions. If these pass, a student submitting the same code gets Accepted.
/// </summary>
[Collection(DockerTestCollection.Name)]
public class PythonRunnerTests
{
    private const string TwoSumReference = """
        import sys
        d = sys.stdin.read().split()
        n, t = int(d[0]), int(d[1])
        a = list(map(int, d[2:2+n]))
        seen = {}
        for i, x in enumerate(a):
            if t - x in seen:
                print(seen[t - x], i)
                break
            seen.setdefault(x, i)
        """;

    [Fact]
    public async Task ReferenceSolution_AgainstTheRealTestCases_IsAccepted()
    {
        var executor = NewExecutor();
        var request = Request(TwoSumReference,
            ("4 9\n2 7 11 15", "0 1"),
            ("3 6\n3 2 4", "1 2"),
            ("2 -8\n-3 -5", "0 1"));

        var outcome = await executor.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.All(outcome.TestCases, tc => Assert.Equal(TestCaseStatus.Passed, tc.Status));
        Assert.Equal(JudgeVerdict.Accepted, VerdictAggregator.Aggregate(request.SubmissionId, outcome).Verdict);
    }

    [Fact]
    public async Task WrongSolution_IsWrongAnswerOnTheTestCaseThatDisagrees()
    {
        var executor = NewExecutor();
        var request = Request("print('0 0')", ("4 9\n2 7 11 15", "0 1"), ("3 6\n3 2 4", "1 2"));

        var outcome = await executor.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(JudgeVerdict.WrongAnswer, VerdictAggregator.Aggregate(request.SubmissionId, outcome).Verdict);
        Assert.Equal("0 0", outcome.TestCases[0].ActualOutput?.Trim());
    }

    [Fact]
    public async Task SyntaxError_IsRuntimeErrorCarryingPythonsMessage()
    {
        var executor = NewExecutor();

        var outcome = await executor.ExecuteAsync(Request("def (:", ("1", "1")), TestContext.Current.CancellationToken);

        var result = Assert.Single(outcome.TestCases);
        Assert.Equal(TestCaseStatus.RuntimeError, result.Status);
        // The submitter needs to see what Python said, not just "it failed".
        Assert.Contains("SyntaxError", result.ErrorMessage);
    }

    [Fact]
    public async Task Exception_IsRuntimeErrorWithTheTraceback()
    {
        var executor = NewExecutor();

        var outcome = await executor.ExecuteAsync(Request("raise ValueError('nope')", ("1", "1")), TestContext.Current.CancellationToken);

        var result = Assert.Single(outcome.TestCases);
        Assert.Equal(TestCaseStatus.RuntimeError, result.Status);
        Assert.Contains("ValueError", result.ErrorMessage);
    }

    [Fact]
    public async Task InfiniteLoop_IsTimeLimitExceeded()
    {
        var executor = NewExecutor();

        var outcome = await executor.ExecuteAsync(
            Request("while True: pass", ("1", "1"), timeLimitMs: 1000),
            TestContext.Current.CancellationToken);

        Assert.Equal(TestCaseStatus.TimeLimitExceeded, Assert.Single(outcome.TestCases).Status);
    }

    [Fact]
    public async Task BruteForceSolution_OnTheLargeTestCase_ExceedsTheTimeLimit()
    {
        var executor = NewExecutor();
        // The O(n^2) version of Two Sum, against the 50,000-element hidden test the content ships
        // precisely to reject it.
        const string bruteForce = """
            import sys
            d = sys.stdin.read().split()
            n, t = int(d[0]), int(d[1])
            a = list(map(int, d[2:2+n]))
            for i in range(n):
                for j in range(i + 1, n):
                    if a[i] + a[j] == t:
                        print(i, j)
                        raise SystemExit
            """;
        var large = string.Join(' ', Enumerable.Range(1, 50000).Select(i => i == 49999 ? 999999999 : i == 50000 ? 999999998 : i));

        var outcome = await executor.ExecuteAsync(
            Request(bruteForce, ($"50000 1999999997\n{large}", "49998 49999"), timeLimitMs: 1000),
            TestContext.Current.CancellationToken);

        Assert.Equal(TestCaseStatus.TimeLimitExceeded, Assert.Single(outcome.TestCases).Status);
    }

    [Fact]
    public async Task Submission_CannotReachTheNetwork()
    {
        var executor = NewExecutor();
        const string phoneHome = """
            import socket
            try:
                socket.create_connection(("1.1.1.1", 80), timeout=2)
                print("ONLINE")
            except Exception:
                print("OFFLINE")
            """;

        var outcome = await executor.ExecuteAsync(Request(phoneHome, ("1", "OFFLINE")), TestContext.Current.CancellationToken);

        Assert.Equal(TestCaseStatus.Passed, Assert.Single(outcome.TestCases).Status);
    }

    /// <summary>Uses the Judge's own shipped configuration, so a broken runner config fails a test.</summary>
    private static DockerSandboxExecutor NewExecutor()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var options = new SandboxOptions();
        configuration.GetSection(SandboxOptions.SectionName).Bind(options);

        Assert.True(options.Runners.ContainsKey("python"), "The shipped configuration must define the python runner.");

        return new DockerSandboxExecutor(
            new DockerClientBuilder().Build(),
            Options.Create(options),
            NullLogger<DockerSandboxExecutor>.Instance);
    }

    private static SubmissionJudgeRequested Request(
        string source,
        params (string Input, string Expected)[] testCases) =>
        Request(source, 5000, testCases);

    private static SubmissionJudgeRequested Request(
        string source,
        (string Input, string Expected) testCase,
        int timeLimitMs = 5000) =>
        Request(source, timeLimitMs, [testCase]);

    private static SubmissionJudgeRequested Request(string source, int timeLimitMs, (string Input, string Expected)[] testCases) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "python",
            source,
            timeLimitMs,
            256,
            [.. testCases.Select((tc, index) => new JudgeTestCase(Guid.NewGuid(), index + 1, tc.Input, tc.Expected))]);
}
