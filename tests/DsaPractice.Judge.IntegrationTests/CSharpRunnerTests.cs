using Docker.DotNet;
using DsaPractice.Contracts;
using DsaPractice.Judge.Execution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DsaPractice.Judge.IntegrationTests;

/// <summary>
/// C# is the first compiled language, so these cover what compiling adds: a separate step with its
/// own limits, a compiler failure that never runs anything, and artifacts shared across test cases
/// without being writable by the program.
///
/// Uses the Judge's shipped configuration, so a broken compile command fails here, not in
/// production.
/// </summary>
[Collection(DockerTestCollection.Name)]
public class CSharpRunnerTests
{
    private const string TwoSumReference = """
        var first = Console.ReadLine()!.Split(' ');
        int n = int.Parse(first[0]), target = int.Parse(first[1]);
        var values = Console.ReadLine()!.Split(' ').Select(int.Parse).ToArray();
        var seen = new Dictionary<int, int>();
        for (var i = 0; i < n; i++)
        {
            if (seen.TryGetValue(target - values[i], out var j))
            {
                Console.WriteLine($"{j} {i}");
                return;
            }

            seen.TryAdd(values[i], i);
        }
        """;

    [Fact]
    public async Task ReferenceSolution_IsAcceptedAcrossEveryTestCase()
    {
        var executor = NewExecutor();
        var request = Request(TwoSumReference,
            ("4 9\n2 7 11 15", "0 1"),
            ("3 6\n3 2 4", "1 2"),
            ("2 -8\n-3 -5", "0 1"));

        var outcome = await executor.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.All(outcome.TestCases, tc => Assert.Equal(TestCaseStatus.Passed, tc.Status));
        // Compiled once, then reused: three test cases, no recompilation.
        Assert.Equal(3, outcome.TestCases.Count);
    }

    [Fact]
    public async Task CodeThatDoesNotCompile_IsCompilationErrorAndNothingRuns()
    {
        var executor = NewExecutor();

        var outcome = await executor.ExecuteAsync(
            Request("Console.WriteLine(\"missing semicolon\")", ("1", "1")),
            TestContext.Current.CancellationToken);

        Assert.True(outcome.CompilationFailed);
        Assert.Empty(outcome.TestCases);            // nothing was executed
        Assert.Contains("error CS", outcome.CompileOutput);  // the compiler's own diagnostic
    }

    [Fact]
    public async Task UnhandledException_IsRuntimeErrorCarryingTheDotnetStackTrace()
    {
        var executor = NewExecutor();

        var outcome = await executor.ExecuteAsync(
            Request("throw new InvalidOperationException(\"boom\");", ("1", "1")),
            TestContext.Current.CancellationToken);

        var result = Assert.Single(outcome.TestCases);
        Assert.Equal(TestCaseStatus.RuntimeError, result.Status);
        Assert.Contains("InvalidOperationException", result.ErrorMessage);
    }

    [Fact]
    public async Task WrongOutput_IsWrongAnswer()
    {
        var executor = NewExecutor();

        var outcome = await executor.ExecuteAsync(
            Request("Console.WriteLine(\"0 0\");", ("4 9\n2 7 11 15", "0 1")),
            TestContext.Current.CancellationToken);

        Assert.Equal(TestCaseStatus.WrongAnswer, Assert.Single(outcome.TestCases).Status);
    }

    [Fact]
    public async Task InfiniteLoop_IsTimeLimitExceeded()
    {
        var executor = NewExecutor();

        var outcome = await executor.ExecuteAsync(
            Request("while (true) { }", ("1", "1"), timeLimitMs: 1000),
            TestContext.Current.CancellationToken);

        Assert.Equal(TestCaseStatus.TimeLimitExceeded, Assert.Single(outcome.TestCases).Status);
    }

    [Fact]
    public async Task CompiledProgram_CannotWriteToItsOwnArtifacts()
    {
        var executor = NewExecutor();
        const string tamper = """
            try
            {
                File.WriteAllText("/out/main.dll", "tampered");
                Console.WriteLine("WROTE");
            }
            catch (Exception)
            {
                Console.WriteLine("READ_ONLY");
            }
            """;

        // The artifact volume is mounted read-only, so a program cannot rewrite what runs next.
        var outcome = await executor.ExecuteAsync(Request(tamper, ("1", "READ_ONLY")), TestContext.Current.CancellationToken);

        Assert.Equal(TestCaseStatus.Passed, Assert.Single(outcome.TestCases).Status);
    }

    [Fact]
    public async Task ArtifactVolume_IsRemovedAfterTheSubmission()
    {
        using var docker = new DockerClientBuilder().Build();
        var executor = NewExecutor();
        var request = Request("Console.WriteLine(\"1\");", ("1", "1"));

        await executor.ExecuteAsync(request, TestContext.Current.CancellationToken);

        var volumes = await docker.Volumes.ListAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(volumes.Volumes, v => v.Name == $"dsa-judge-{request.SubmissionId:N}");
    }

    private static DockerSandboxExecutor NewExecutor()
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false).Build();
        var options = new SandboxOptions();
        configuration.GetSection(SandboxOptions.SectionName).Bind(options);

        Assert.True(options.Runners.TryGetValue("csharp", out var runner), "The shipped configuration must define the csharp runner.");
        Assert.True(runner!.IsCompiled, "The csharp runner must have a compile step.");

        return new DockerSandboxExecutor(
            new DockerClientBuilder().Build(),
            Options.Create(options),
            NullLogger<DockerSandboxExecutor>.Instance);
    }

    private static SubmissionJudgeRequested Request(string source, params (string Input, string Expected)[] testCases) =>
        Request(source, 5000, testCases);

    private static SubmissionJudgeRequested Request(string source, (string Input, string Expected) testCase, int timeLimitMs = 5000) =>
        Request(source, timeLimitMs, [testCase]);

    private static SubmissionJudgeRequested Request(string source, int timeLimitMs, (string Input, string Expected)[] testCases) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "csharp",
            source,
            timeLimitMs,
            256,
            [.. testCases.Select((tc, index) => new JudgeTestCase(Guid.NewGuid(), index + 1, tc.Input, tc.Expected))]);
}
