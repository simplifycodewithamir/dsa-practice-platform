namespace DsaPractice.Judge.Execution;

/// <summary>
/// Limits applied to every sandbox container. These are the values proposed for review (the
/// project skill requires per-runner limits to be proposed, not hardcoded silently) -- all of them
/// are configuration, so they can be tuned per deployment without a code change.
/// </summary>
public sealed class SandboxOptions
{
    public const string SectionName = "Judge:Sandbox";

    /// <summary>
    /// CPU cores per run. One core means a wall-clock limit is a meaningful proxy for CPU time,
    /// and one submission cannot starve the others on a small VM (decision D1: a 4-core free tier).
    /// </summary>
    public double CpuCores { get; init; } = 1.0;

    /// <summary>
    /// Processes allowed inside the container. 64 is comfortable for an interpreter plus threads,
    /// and low enough that a fork bomb hits the limit instead of the host.
    /// </summary>
    public int PidsLimit { get; init; } = 64;

    /// <summary>
    /// Added to the question's own time limit before the container is killed. Container start,
    /// interpreter boot and teardown are not the submitter's fault, so they must not count against
    /// their time limit -- but they still need a ceiling.
    /// </summary>
    public int StartupGraceMs { get; init; } = 3000;

    /// <summary>Bytes of stdout/stderr kept per run; a program that floods output is capped, not obeyed.</summary>
    public int MaxOutputBytes { get; init; } = 64 * 1024;

    /// <summary>Writable scratch space mounted at /work, in megabytes. The root filesystem is read-only.</summary>
    public int WorkspaceSizeMb { get; init; } = 32;

    /// <summary>How long to wait for an image pull before giving up.</summary>
    public int ImagePullTimeoutSeconds { get; init; } = 300;

    /// <summary>Memory for a compile step, which needs far more than the program it produces.</summary>
    public int CompileMemoryMb { get; init; } = 1024;

    /// <summary>Ceiling on a compile step. Compiling is not the submitter's time limit.</summary>
    public int CompileTimeoutMs { get; init; } = 30_000;

    /// <summary>Language key (as submitted) to how it is run.</summary>
    public Dictionary<string, LanguageRunner> Runners { get; init; } = [];
}

/// <summary>How one language is executed inside the sandbox.</summary>
public sealed class LanguageRunner
{
    /// <summary>Image to run. Pinned by tag; the Judge pulls it once and reuses it.</summary>
    public required string Image { get; init; }

    /// <summary>Filename the submitted source is written as, inside /work.</summary>
    public required string SourceFileName { get; init; }

    /// <summary>Command that runs it, e.g. ["python", "/work/main.py"].</summary>
    public required string[] RunCommand { get; init; }

    /// <summary>
    /// Multiplies the question's time limit for this language. A question's limit is written with
    /// a native-speed solution in mind, so an interpreter needs more of it for the same algorithm.
    /// </summary>
    public double TimeLimitMultiplier { get; init; } = 1.0;

    /// <summary>
    /// Image the compile step runs in, when the language has one. Usually a full SDK, while
    /// <see cref="Image"/> is the much smaller runtime the compiled program actually needs.
    /// </summary>
    public string? CompileImage { get; init; }

    /// <summary>
    /// Compile command, or null for an interpreted language. It writes its artifacts to
    /// <see cref="ArtifactPath"/>, which the run containers then mount read-only.
    /// </summary>
    public string[]? CompileCommand { get; init; }

    /// <summary>Where compiled artifacts are written and later mounted.</summary>
    public string ArtifactPath { get; init; } = "/out";

    public bool IsCompiled => CompileCommand is { Length: > 0 } && CompileImage is not null;
}
