using System.Diagnostics;
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using DsaPractice.Contracts;
using Microsoft.Extensions.Options;

namespace DsaPractice.Judge.Execution;

/// <summary>
/// Runs a submission in one throwaway container per test case.
///
/// One container per test case, never reused: a program that corrupts its own filesystem, leaves a
/// daemon running or fills /work cannot affect the next test case, and each run starts from the
/// same state so a verdict is reproducible.
///
/// Every container is created with the network off, a read-only root filesystem, all capabilities
/// dropped, privilege escalation blocked, a pids limit, a memory limit and a CPU quota. The one
/// writable place is a small tmpfs at /work, which never touches the host disk.
/// </summary>
public sealed class DockerSandboxExecutor(
    IDockerClient docker,
    IOptions<SandboxOptions> options,
    ILogger<DockerSandboxExecutor> logger) : ISandboxExecutor
{
    private const string SourceEnvironmentVariable = "DSA_SOURCE_B64";

    /// <summary>Encoded length; Linux rejects an environment variable beyond roughly 128 KB.</summary>
    private const int MaxSourceEnvironmentLength = 100_000;

    public async Task<ExecutionOutcome> ExecuteAsync(SubmissionJudgeRequested request, CancellationToken cancellationToken)
    {
        var sandbox = options.Value;

        if (!sandbox.Runners.TryGetValue(request.Language, out var runner))
        {
            // Validation at the edge should have rejected this long before it was queued.
            throw new NotSupportedException($"No sandbox runner is configured for language '{request.Language}'.");
        }

        await EnsureImageAsync(runner.Image, cancellationToken);

        var outcomes = new List<TestCaseOutcome>(request.TestCases.Count);

        foreach (var testCase in request.TestCases.OrderBy(tc => tc.Ordinal))
        {
            var outcome = await RunTestCaseAsync(request, runner, testCase, cancellationToken);
            outcomes.Add(outcome);

            // The verdict is decided by the first failure, so the remaining cases cannot change it
            // -- and running them would spend sandbox time on an answer we already have.
            if (outcome.Status != TestCaseStatus.Passed)
            {
                break;
            }
        }

        return new ExecutionOutcome(outcomes);
    }

    private async Task<TestCaseOutcome> RunTestCaseAsync(
        SubmissionJudgeRequested request,
        LanguageRunner runner,
        JudgeTestCase testCase,
        CancellationToken cancellationToken)
    {
        var sandbox = options.Value;
        var timeLimit = TimeSpan.FromMilliseconds(request.TimeLimitMs * runner.TimeLimitMultiplier);
        var wallClockLimit = timeLimit + TimeSpan.FromMilliseconds(sandbox.StartupGraceMs);

        var containerId = await CreateContainerAsync(request, runner, cancellationToken);

        try
        {
            using var attach = await docker.Containers.AttachContainerAsync(
                containerId,
                new ContainerAttachParameters { Stream = true, Stdin = true, Stdout = true, Stderr = true },
                cancellationToken);

            var stopwatch = Stopwatch.StartNew();
            await docker.Containers.StartContainerAsync(containerId, new ContainerStartParameters(), cancellationToken);

            await WriteStdinAsync(attach, testCase.Input, cancellationToken);

            using var runTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            runTimeout.CancelAfter(wallClockLimit);

            string stdout;
            string stderr;
            try
            {
                (stdout, stderr) = await attach.ReadOutputToEndAsync(runTimeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                logger.LogInformation(
                    "Submission {SubmissionId} test {Ordinal} exceeded {WallClockLimit}.",
                    request.SubmissionId, testCase.Ordinal, wallClockLimit);

                return Failure(testCase, TestCaseStatus.TimeLimitExceeded, null, "Time limit exceeded.", (long)timeLimit.TotalMilliseconds);
            }

            var wait = await docker.Containers.WaitContainerAsync(containerId, cancellationToken);
            stopwatch.Stop();

            var inspection = await docker.Containers.InspectContainerAsync(containerId, cancellationToken);

            // Timed by the container's own start and finish, not by the wall clock around the whole
            // call: creating and starting a container costs hundreds of milliseconds, and charging
            // the submitter for the daemon's overhead would fail correct solutions on a busy host.
            // The wall clock still governs when the container is killed (time limit + grace).
            var elapsedMs = ExecutionTimeMs(inspection, stopwatch);

            if (inspection.State?.OOMKilled == true)
            {
                return Failure(testCase, TestCaseStatus.MemoryLimitExceeded, null, "Memory limit exceeded.", elapsedMs);
            }

            if (elapsedMs > timeLimit.TotalMilliseconds)
            {
                return Failure(testCase, TestCaseStatus.TimeLimitExceeded, null, "Time limit exceeded.", elapsedMs);
            }

            if (wait.StatusCode != 0)
            {
                return Failure(testCase, TestCaseStatus.RuntimeError, Cap(stdout), Cap(stderr), elapsedMs);
            }

            var actual = Cap(stdout);
            return OutputComparer.Matches(actual, testCase.ExpectedOutput)
                ? new TestCaseOutcome(testCase.TestCaseId, testCase.Ordinal, TestCaseStatus.Passed, actual, null, elapsedMs)
                : Failure(testCase, TestCaseStatus.WrongAnswer, actual, null, elapsedMs);
        }
        finally
        {
            await RemoveContainerAsync(containerId);
        }
    }

    private async Task<string> CreateContainerAsync(SubmissionJudgeRequested request, LanguageRunner runner, CancellationToken cancellationToken)
    {
        var sandbox = options.Value;
        var memoryBytes = (long)request.MemoryLimitMb * 1024 * 1024;

        var created = await docker.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Image = runner.Image,
                Cmd = BuildCommand(runner, request.SourceCode, out var sourceEnvironmentValue),
                WorkingDir = "/work",
                // Never root, even inside a locked-down container.
                User = "65534:65534",
                AttachStdin = true,
                AttachStdout = true,
                AttachStderr = true,
                OpenStdin = true,
                StdinOnce = true,
                NetworkDisabled = true,
                Env = ["HOME=/work", $"{SourceEnvironmentVariable}={sourceEnvironmentValue}"],
                HostConfig = new HostConfig
                {
                    // No network at all: submitted code cannot call home, mine, or reach the host.
                    NetworkMode = "none",
                    Memory = memoryBytes,
                    // Equal to Memory disables swap: without this the limit is soft, because the
                    // kernel would let the container swap instead of being OOM-killed.
                    MemorySwap = memoryBytes,
                    NanoCPUs = (long)(sandbox.CpuCores * 1_000_000_000),
                    PidsLimit = sandbox.PidsLimit,
                    ReadonlyRootfs = true,
                    // The only writable place, in memory, capped, and gone when the container is.
                    // mode=1777 because the container runs as nobody: created through the API a
                    // tmpfs is root-owned and unwritable by anyone else, unlike the CLI's default.
                    Tmpfs = new Dictionary<string, string>
                    {
                        ["/work"] = $"rw,exec,nosuid,mode=1777,size={sandbox.WorkspaceSizeMb}m",
                        ["/tmp"] = $"rw,noexec,nosuid,mode=1777,size={sandbox.WorkspaceSizeMb}m"
                    },
                    CapDrop = ["ALL"],
                    SecurityOpt = ["no-new-privileges"],
                    AutoRemove = false // removed explicitly, so a failure to start can still be inspected
                }
            },
            cancellationToken);

        logger.LogDebug(
            "Created sandbox {ContainerId} for submission {SubmissionId} ({Memory} MB, {Cpu} CPU, pids {Pids}).",
            created.ID[..12], request.SubmissionId, request.MemoryLimitMb, sandbox.CpuCores, sandbox.PidsLimit);

        return created.ID;
    }

    /// <summary>
    /// The source is handed over as a base64 environment variable and written by the container into
    /// its own tmpfs, rather than copied in with the archive API.
    ///
    /// Two reasons. Docker refuses to copy into a container whose root filesystem is read-only, and
    /// a read-only root is worth more than the convenience; and a bind mount would be worse still,
    /// since it would expose a host path to submitted code (and would not even resolve correctly
    /// when the Judge itself runs in a container against the host daemon).
    ///
    /// base64 so that no quoting, newline or non-UTF8 byte in the submission can break out of the
    /// shell command.
    /// </summary>
    private static string[] BuildCommand(LanguageRunner runner, string sourceCode, out string sourceEnvironmentValue)
    {
        sourceEnvironmentValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(sourceCode));

        if (sourceEnvironmentValue.Length > MaxSourceEnvironmentLength)
        {
            // Linux caps a single environment variable at ~128 KB; a submission this large is
            // rejected here rather than being silently truncated into something that won't compile.
            throw new InvalidOperationException(
                $"Submitted source is too large to run ({sourceEnvironmentValue.Length} bytes encoded).");
        }

        var runCommand = string.Join(' ', runner.RunCommand.Select(Quote));

        return
        [
            "/bin/sh",
            "-c",
            $"set -e; printf '%s' \"${SourceEnvironmentVariable}\" | base64 -d > /work/{runner.SourceFileName}; exec {runCommand}"
        ];
    }

    private static string Quote(string argument) => $"'{argument.Replace("'", "'\\''")}'";

    private static async Task WriteStdinAsync(MultiplexedStream attach, string input, CancellationToken cancellationToken)
    {
        // Test case inputs are stored without a trailing newline; a program reading a line expects
        // one, and would otherwise block until the time limit.
        var bytes = Encoding.UTF8.GetBytes(input.EndsWith('\n') ? input : input + "\n");
        await attach.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
        attach.CloseWrite();
    }

    private async Task EnsureImageAsync(string image, CancellationToken cancellationToken)
    {
        var existing = await docker.Images.ListImagesAsync(
            new ImagesListParameters { Filters = new Dictionary<string, IDictionary<string, bool>> { ["reference"] = new Dictionary<string, bool> { [image] = true } } },
            cancellationToken);

        if (existing.Count > 0)
        {
            return;
        }

        logger.LogInformation("Pulling sandbox image {Image}.", image);

        using var pullTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        pullTimeout.CancelAfter(TimeSpan.FromSeconds(options.Value.ImagePullTimeoutSeconds));

        await docker.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = image },
            authConfig: null,
            new Progress<JSONMessage>(),
            pullTimeout.Token);
    }

    private async Task RemoveContainerAsync(string containerId)
    {
        try
        {
            // Not cancellable on purpose: a cancelled judge run must still take its container with
            // it, or the host accumulates them.
            await docker.Containers.RemoveContainerAsync(
                containerId,
                new ContainerRemoveParameters { Force = true, RemoveVolumes = true },
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to remove sandbox container {ContainerId}.", containerId[..12]);
        }
    }

    private static long ExecutionTimeMs(ContainerInspectResponse inspection, Stopwatch fallback)
    {
        // The daemon reports these as RFC3339 strings.
        return DateTimeOffset.TryParse(inspection.State?.StartedAt, out var start) &&
               DateTimeOffset.TryParse(inspection.State?.FinishedAt, out var finish) &&
               finish > start
            ? (long)(finish - start).TotalMilliseconds
            : fallback.ElapsedMilliseconds;
    }

    private string? Cap(string? output)
    {
        if (output is null)
        {
            return null;
        }

        var max = options.Value.MaxOutputBytes;
        return output.Length <= max ? output : output[..max];
    }

    private static TestCaseOutcome Failure(JudgeTestCase testCase, TestCaseStatus status, string? actual, string? error, long elapsedMs) =>
        new(testCase.TestCaseId, testCase.Ordinal, status, actual, error, elapsedMs);
}
