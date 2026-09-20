using DsaPractice.Judge.Execution;
using Xunit;

namespace DsaPractice.Judge.UnitTests.Execution;

/// <summary>
/// These defaults are what applies when a deployment configures nothing, so they are the weakest
/// sandbox this service will ever run. They are asserted here because a default weakened by
/// accident would not fail any other test -- the integration suite supplies its own options.
/// </summary>
public class SandboxOptionsTests
{
    [Fact]
    public void Defaults_CapCpuPidsAndOutput()
    {
        var options = new SandboxOptions();

        Assert.Equal(1.0, options.CpuCores);        // one submission cannot starve the others
        Assert.Equal(64, options.PidsLimit);        // a fork bomb hits this, not the host
        Assert.Equal(64 * 1024, options.MaxOutputBytes);
        Assert.Equal(32, options.WorkspaceSizeMb);
    }

    [Fact]
    public void Defaults_SeccompIsTheDaemonProfile_NotUnconfined()
    {
        // "unconfined" would hand submitted code the full syscall surface.
        Assert.Equal("default", new SandboxOptions().SeccompProfile);
        Assert.NotEqual("unconfined", new SandboxOptions().SeccompProfile);
    }

    [Fact]
    public void Defaults_NoRunnersAreConfigured()
    {
        // Nothing runs until a deployment says what may run, and how.
        Assert.Empty(new SandboxOptions().Runners);
    }

    [Fact]
    public void Defaults_CompileIsBoundedAndGetsMoreMemoryThanTheProgramItProduces()
    {
        var options = new SandboxOptions();

        Assert.True(options.CompileTimeoutMs > 0);
        Assert.Equal(30_000, options.CompileTimeoutMs);
        Assert.Equal(1024, options.CompileMemoryMb);
    }

    [Fact]
    public void Defaults_StartupGraceIsPositiveButNotGenerous()
    {
        // Container start is not the submitter's fault, so it is not charged to them -- but it
        // still has a ceiling, or an infinite loop would never be killed.
        var options = new SandboxOptions();

        Assert.InRange(options.StartupGraceMs, 1, 10_000);
    }

    [Fact]
    public void SectionName_IsTheNestedJudgeSandboxSection()
    {
        Assert.Equal("Judge:Sandbox", SandboxOptions.SectionName);
    }
}
