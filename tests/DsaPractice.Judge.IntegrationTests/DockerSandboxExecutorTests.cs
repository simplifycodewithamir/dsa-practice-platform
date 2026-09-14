using Docker.DotNet;
using DsaPractice.Contracts;
using DsaPractice.Judge.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DsaPractice.Judge.IntegrationTests;

/// <summary>
/// Runs against the real Docker daemon, because every claim worth making here -- that the network
/// is off, that a fork bomb hits a pids limit, that an over-allocating program is OOM-killed -- is
/// a claim about the kernel and the daemon, not about our code.
///
/// The "language" under test is busybox sh, which keeps the image tiny and makes the hostile
/// programs short. Item 12 adds the real Python runner.
/// </summary>
[Collection(DockerTestCollection.Name)]
public class DockerSandboxExecutorTests
{
    private const string ShellImage = "busybox:1.36";

    [Fact]
    public async Task Execute_CorrectOutput_Passes()
    {
        var executor = NewExecutor();

        var outcome = await executor.ExecuteAsync(Request("read a b; echo $((a+b))", ("2 3", "5")), TestContext.Current.CancellationToken);

        var result = Assert.Single(outcome.TestCases);
        Assert.Equal(TestCaseStatus.Passed, result.Status);
        Assert.Equal("5", result.ActualOutput?.Trim());
    }

    [Fact]
    public async Task Execute_WrongOutput_IsWrongAnswerAndKeepsWhatWasPrinted()
    {
        var executor = NewExecutor();

        var outcome = await executor.ExecuteAsync(Request("echo 42", ("2 3", "5")), TestContext.Current.CancellationToken);

        var result = Assert.Single(outcome.TestCases);
        Assert.Equal(TestCaseStatus.WrongAnswer, result.Status);
        Assert.Equal("42", result.ActualOutput?.Trim());
    }

    [Fact]
    public async Task Execute_NonZeroExit_IsRuntimeErrorCarryingStderr()
    {
        var executor = NewExecutor();

        var outcome = await executor.ExecuteAsync(Request("echo 'it broke' >&2; exit 3", ("1", "1")), TestContext.Current.CancellationToken);

        var result = Assert.Single(outcome.TestCases);
        Assert.Equal(TestCaseStatus.RuntimeError, result.Status);
        Assert.Contains("it broke", result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_InfiniteLoop_IsTimeLimitExceededAndTheContainerIsGone()
    {
        var executor = NewExecutor();
        var before = await ContainerCountAsync();

        var outcome = await executor.ExecuteAsync(Request("while true; do :; done", ("1", "1"), timeLimitMs: 1000), TestContext.Current.CancellationToken);

        Assert.Equal(TestCaseStatus.TimeLimitExceeded, Assert.Single(outcome.TestCases).Status);
        // Torn down even though it never finished on its own.
        Assert.Equal(before, await ContainerCountAsync());
    }

    [Fact]
    public async Task Execute_AllocatesFarBeyondTheMemoryLimit_DoesNotPass()
    {
        var executor = NewExecutor();

        // Writing into the in-memory /work counts against the container's memory limit.
        var outcome = await executor.ExecuteAsync(
            Request("dd if=/dev/zero of=/work/fill bs=1M count=512 2>/dev/null; echo done", ("1", "done"), memoryLimitMb: 32),
            TestContext.Current.CancellationToken);

        var result = Assert.Single(outcome.TestCases);
        Assert.NotEqual(TestCaseStatus.Passed, result.Status);
    }

    [Fact]
    public async Task Execute_NetworkAccess_Fails()
    {
        var executor = NewExecutor();

        // Nothing to resolve and nothing to route to: the container has no network at all.
        var outcome = await executor.ExecuteAsync(
            Request("wget -T 2 -q -O- http://example.com || echo NO_NETWORK", ("1", "NO_NETWORK")),
            TestContext.Current.CancellationToken);

        Assert.Equal(TestCaseStatus.Passed, Assert.Single(outcome.TestCases).Status);
    }

    [Fact]
    public async Task Execute_WritingOutsideTheWorkspace_Fails()
    {
        var executor = NewExecutor();

        var outcome = await executor.ExecuteAsync(
            Request("touch /evil 2>/dev/null && echo WROTE || echo READ_ONLY", ("1", "READ_ONLY")),
            TestContext.Current.CancellationToken);

        Assert.Equal(TestCaseStatus.Passed, Assert.Single(outcome.TestCases).Status);
    }

    [Fact]
    public async Task Execute_RunsAsNonRoot()
    {
        var executor = NewExecutor();

        var outcome = await executor.ExecuteAsync(Request("id -u", ("1", "65534")), TestContext.Current.CancellationToken);

        Assert.Equal(TestCaseStatus.Passed, Assert.Single(outcome.TestCases).Status);
    }

    [Fact]
    public async Task Execute_ForkBomb_IsContainedAndTheHostSurvives()
    {
        var executor = NewExecutor();
        var before = await ContainerCountAsync();

        var outcome = await executor.ExecuteAsync(
            Request(":(){ :|:& };:", ("1", "1"), timeLimitMs: 2000),
            TestContext.Current.CancellationToken);

        // Whatever it ends up as -- killed, timed out, failed -- it must not pass, and the
        // container must be gone afterwards.
        Assert.NotEqual(TestCaseStatus.Passed, Assert.Single(outcome.TestCases).Status);
        Assert.Equal(before, await ContainerCountAsync());
    }

    [Fact]
    public async Task Execute_OutputFlood_IsCappedNotObeyed()
    {
        var executor = NewExecutor();

        var outcome = await executor.ExecuteAsync(
            Request("yes 0123456789 | head -c 10000000", ("1", "x"), timeLimitMs: 5000),
            TestContext.Current.CancellationToken);

        var result = Assert.Single(outcome.TestCases);
        Assert.NotEqual(TestCaseStatus.Passed, result.Status);
        Assert.True(result.ActualOutput is null || result.ActualOutput.Length <= 64 * 1024,
            "Captured output must be capped regardless of how much the program printed.");
    }

    [Fact]
    public async Task Execute_StopsAtTheFirstFailingTestCase()
    {
        var executor = NewExecutor();
        var request = Request("read a; echo $a", ("1", "1"), ("2", "WRONG"), ("3", "3"));

        var outcome = await executor.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // The verdict is decided by the first failure, so running the rest buys nothing.
        Assert.Equal(2, outcome.TestCases.Count);
        Assert.Equal(TestCaseStatus.WrongAnswer, outcome.TestCases[1].Status);
    }

    [Fact]
    public async Task Execute_UnknownLanguage_Throws()
    {
        var executor = NewExecutor();
        var request = Request("echo 1", ("1", "1")) with { Language = "brainfuck" };

        await Assert.ThrowsAsync<NotSupportedException>(() => executor.ExecuteAsync(request, TestContext.Current.CancellationToken));
    }

    private static DockerSandboxExecutor NewExecutor()
    {
        var options = new SandboxOptions
        {
            Runners =
            {
                ["shell"] = new LanguageRunner
                {
                    Image = ShellImage,
                    SourceFileName = "main.sh",
                    RunCommand = ["sh", "/work/main.sh"]
                }
            }
        };

        return new DockerSandboxExecutor(
            new DockerClientBuilder().Build(),
            Options.Create(options),
            NullLogger<DockerSandboxExecutor>.Instance);
    }

    private static SubmissionJudgeRequested Request(
        string source,
        params (string Input, string Expected)[] testCases) =>
        Request(source, 5000, 256, testCases);

    private static SubmissionJudgeRequested Request(
        string source,
        (string Input, string Expected) testCase,
        int timeLimitMs = 5000,
        int memoryLimitMb = 256) =>
        Request(source, timeLimitMs, memoryLimitMb, [testCase]);

    private static SubmissionJudgeRequested Request(
        string source,
        int timeLimitMs,
        int memoryLimitMb,
        (string Input, string Expected)[] testCases) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "shell",
            source,
            timeLimitMs,
            memoryLimitMb,
            [.. testCases.Select((tc, index) => new JudgeTestCase(Guid.NewGuid(), index + 1, tc.Input, tc.Expected))]);

    private static async Task<int> ContainerCountAsync()
    {
        using var docker = new DockerClientBuilder().Build();
        var containers = await docker.Containers.ListContainersAsync(
            new Docker.DotNet.Models.ContainersListParameters { All = true },
            TestContext.Current.CancellationToken);
        return containers.Count;
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DockerTestCollection
{
    // Sandbox tests count containers and push the host around; running them in parallel with each
    // other would make both of those meaningless.
    public const string Name = "Docker sandbox tests";
}
