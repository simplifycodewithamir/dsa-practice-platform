using DsaPractice.Judge.Execution;
using Xunit;

namespace DsaPractice.Judge.UnitTests.Execution;

/// <summary>
/// <see cref="LanguageRunner.IsCompiled"/> decides whether a submission gets a compile step and an
/// artifact volume at all. A half-configured runner that answered "yes" would send the executor
/// looking for a compile image it does not have; one that answered "no" would try to run C# source
/// as if it were a script. Both are configuration mistakes, so the property has to be strict.
/// </summary>
public class LanguageRunnerTests
{
    [Fact]
    public void IsCompiled_CompileCommandAndImage_IsTrue()
    {
        var runner = NewRunner(compileImage: "dotnet/sdk:10.0", compileCommand: ["dotnet", "publish"]);

        Assert.True(runner.IsCompiled);
    }

    [Fact]
    public void IsCompiled_NeitherConfigured_IsFalse()
    {
        // An interpreted language: python runs its source directly.
        Assert.False(NewRunner().IsCompiled);
    }

    [Theory]
    [InlineData("dotnet/sdk:10.0", null)]     // an image with nothing to run in it
    [InlineData(null, new[] { "dotnet" })]    // a command with nowhere to run it
    public void IsCompiled_HalfConfigured_IsFalse(string? compileImage, string[]? compileCommand)
    {
        Assert.False(NewRunner(compileImage, compileCommand).IsCompiled);
    }

    [Fact]
    public void IsCompiled_EmptyCompileCommand_IsFalse()
    {
        // An empty array is "nothing to execute", not "compile with no arguments".
        Assert.False(NewRunner("dotnet/sdk:10.0", []).IsCompiled);
    }

    [Fact]
    public void Defaults_AreTheInterpretedCase()
    {
        var runner = NewRunner();

        Assert.Equal(1.0, runner.TimeLimitMultiplier);
        Assert.Equal("/out", runner.ArtifactPath);
    }

    private static LanguageRunner NewRunner(string? compileImage = null, string[]? compileCommand = null) => new()
    {
        Image = "python:3.13-alpine",
        SourceFileName = "main.py",
        RunCommand = ["python", "/work/main.py"],
        CompileImage = compileImage,
        CompileCommand = compileCommand
    };
}
