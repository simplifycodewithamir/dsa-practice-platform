using Docker.DotNet;
using DsaPractice.Contracts;
using DsaPractice.Judge.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DsaPractice.Judge.IntegrationTests;

/// <summary>
/// Submits code that tries to get out. Each test states what an attacker would gain and asserts
/// the kernel refuses it -- these are claims about namespaces, capabilities and seccomp, so they
/// are made against the real daemon or not at all.
///
/// The submitted programs report their findings on stdout, and the expected output is the safe
/// answer, so a test fails loudly the moment an escape starts working.
/// </summary>
[Collection(DockerTestCollection.Name)]
public class SandboxEscapeTests
{
    [Fact]
    public async Task Submission_CannotSeeTheDockerSocket()
    {
        // The socket is root-equivalent on the host: reaching it would mean starting a privileged
        // container and owning the machine.
        await AssertSandboxAsync(
            "if [ -S /var/run/docker.sock ]; then echo SOCKET_VISIBLE; else echo NO_SOCKET; fi",
            "NO_SOCKET");
    }

    [Fact]
    public async Task Submission_HasNoCapabilities()
    {
        // CapEff 0 means no CAP_SYS_ADMIN, no CAP_NET_RAW, nothing to escalate with.
        await AssertSandboxAsync(
            "grep CapEff /proc/self/status | grep -q '0000000000000000' && echo NO_CAPS || echo HAS_CAPS",
            "NO_CAPS");
    }

    [Fact]
    public async Task Submission_CannotRegainPrivileges()
    {
        // NoNewPrivs defeats setuid binaries: even a setuid-root helper cannot raise privileges.
        await AssertSandboxAsync(
            "grep NoNewPrivs /proc/self/status | grep -q 1 && echo NO_NEW_PRIVS || echo CAN_ESCALATE",
            "NO_NEW_PRIVS");
    }

    [Fact]
    public async Task Submission_CannotSeeOtherProcesses()
    {
        // Its own PID namespace: no host processes, and no other submission's process, to signal,
        // trace or read memory from.
        await AssertSandboxAsync(
            "test \"$(ls /proc | grep -c '^[0-9]*$')\" -lt 10 && echo ISOLATED || echo SEES_HOST_PROCESSES",
            "ISOLATED");
    }

    [Fact]
    public async Task Submission_CannotMountAnything()
    {
        // Mounting would be the classic path out: bind the host root in and read its filesystem.
        await AssertSandboxAsync(
            "mkdir -p /work/m 2>/dev/null; mount -t proc proc /work/m 2>/dev/null && echo MOUNTED || echo MOUNT_DENIED",
            "MOUNT_DENIED");
    }

    [Fact]
    public async Task Submission_CannotWriteToKernelInterfaces()
    {
        // /proc/sysrq-trigger can reboot the host; /proc and /sys must be read-only here.
        await AssertSandboxAsync(
            "echo s > /proc/sysrq-trigger 2>/dev/null && echo WROTE_SYSRQ || echo KERNEL_READONLY",
            "KERNEL_READONLY");
    }

    [Fact]
    public async Task Submission_CannotReadHostDevices()
    {
        // Reading the host's disk device would bypass the filesystem entirely.
        await AssertSandboxAsync(
            "dd if=/dev/sda bs=1 count=1 of=/dev/null 2>/dev/null && echo READ_DISK || echo NO_DEVICE_ACCESS",
            "NO_DEVICE_ACCESS");
    }

    [Fact]
    public async Task Submission_RaisingItsOwnRlimits_StillCannotExceedThePidsCap()
    {
        // A process may raise its own soft rlimit up to the hard limit, and that is fine: the cap
        // that actually holds is the cgroup's, which the process cannot touch from inside. This
        // pins that distinction, because "ulimit succeeded" reads like a hole and isn't one.
        // It only prints if it managed to spawn all 300, which the cgroup cap prevents; in
        // practice the shell cannot even fork far enough to report, which is the cap working.
        var outcome = await ExecuteAsync(
            "ulimit -n 999999 2>/dev/null; n=0; while [ $n -lt 300 ]; do sleep 30 & n=$((n+1)); done; echo SPAWNED_ALL",
            "SPAWNED_ALL");

        Assert.NotEqual(TestCaseStatus.Passed, Assert.Single(outcome.TestCases).Status);
    }

    [Fact]
    public async Task Submission_CannotKeepAProcessAliveAfterTheRun()
    {
        using var docker = new DockerClientBuilder().Build();
        var before = await ContainerCountAsync(docker);

        // A daemon left behind would keep consuming the host's CPU after the verdict.
        await ExecuteAsync("(while true; do :; done) & echo STARTED_BACKGROUND", "STARTED_BACKGROUND");

        // The container is destroyed with everything in it, background processes included.
        Assert.Equal(before, await ContainerCountAsync(docker));
    }

    private static async Task AssertSandboxAsync(string program, string expected)
    {
        var outcome = await ExecuteAsync(program, expected);
        var result = Assert.Single(outcome.TestCases);

        Assert.True(
            result.Status == TestCaseStatus.Passed,
            $"Expected the sandbox to report '{expected}'; got {result.Status} with output '{result.ActualOutput}' and error '{result.ErrorMessage}'.");
    }

    private static async Task<ExecutionOutcome> ExecuteAsync(string program, string expected)
    {
        var options = new SandboxOptions
        {
            Runners =
            {
                ["shell"] = new LanguageRunner
                {
                    Image = "busybox:1.36",
                    SourceFileName = "main.sh",
                    RunCommand = ["sh", "/work/main.sh"]
                }
            }
        };

        var executor = new DockerSandboxExecutor(
            new DockerClientBuilder().Build(),
            Options.Create(options),
            NullLogger<DockerSandboxExecutor>.Instance);

        var request = new SubmissionJudgeRequested(
            Guid.NewGuid(), Guid.NewGuid(), "shell", program, 5000, 256,
            [new JudgeTestCase(Guid.NewGuid(), 1, "", expected)]);

        return await executor.ExecuteAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<int> ContainerCountAsync(IDockerClient docker)
    {
        var containers = await docker.Containers.ListContainersAsync(
            new Docker.DotNet.Models.ContainersListParameters { All = true },
            TestContext.Current.CancellationToken);
        return containers.Count;
    }
}
