using Docker.DotNet;
using DsaPractice.Contracts;
using DsaPractice.Judge.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DsaPractice.Judge.UnitTests.Execution;

/// <summary>
/// What the executor decides before it ever talks to the daemon. Everything past that point runs
/// real containers and is covered by DsaPractice.Judge.IntegrationTests.
/// </summary>
public class DockerSandboxExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_LanguageWithNoConfiguredRunner_Throws()
    {
        var executor = NewExecutor(out _);

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => executor.ExecuteAsync(NewRequest("rust"), CancellationToken.None));

        Assert.Contains("rust", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_LanguageWithNoConfiguredRunner_NeverReachesTheDaemon()
    {
        // A message that got past validation must be rejected before it costs an image pull or a
        // container, not halfway through one.
        var executor = NewExecutor(out var docker);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => executor.ExecuteAsync(NewRequest("rust"), CancellationToken.None));

        docker.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_LanguageDifferingOnlyInCase_IsNotSupported()
    {
        // Runner keys are matched exactly, so "Python" is not the configured "python".
        var executor = NewExecutor(out _);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => executor.ExecuteAsync(NewRequest("Python"), CancellationToken.None));
    }

    private static DockerSandboxExecutor NewExecutor(out Mock<IDockerClient> docker)
    {
        docker = new Mock<IDockerClient>(MockBehavior.Strict);

        var options = Options.Create(new SandboxOptions
        {
            Runners = new Dictionary<string, LanguageRunner>
            {
                ["python"] = new()
                {
                    Image = "python:3.13-alpine",
                    SourceFileName = "main.py",
                    RunCommand = ["python", "/work/main.py"]
                }
            }
        });

        return new DockerSandboxExecutor(docker.Object, options, NullLogger<DockerSandboxExecutor>.Instance);
    }

    private static SubmissionJudgeRequested NewRequest(string language) => new(
        SubmissionId: Guid.NewGuid(),
        QuestionId: Guid.NewGuid(),
        Language: language,
        SourceCode: "print(1)",
        TimeLimitMs: 1000,
        MemoryLimitMb: 128,
        TestCases: [new JudgeTestCase(Guid.NewGuid(), 1, "1", "1")]);
}
