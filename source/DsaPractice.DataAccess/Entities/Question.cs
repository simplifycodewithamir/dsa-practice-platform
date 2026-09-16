using DsaPractice.DataAccess.Enums;

namespace DsaPractice.DataAccess.Entities;

public sealed class Question
{
    // Lowercase kebab-case ("two-sum"). Enforced as a DB check constraint and reused by the Api's
    // route constraint, so a malformed slug can neither be stored nor routed.
    public const string SlugPattern = "^[a-z0-9]+(-[a-z0-9]+)*$";
    public const int SlugMaxLength = 100;

    public Guid Id { get; set; }
    public required string Slug { get; set; } // stable public URL key -- /problems/{slug}
    public required string Title { get; set; }
    public required string Description { get; set; } // problem statement, markdown
    public required QuestionDifficulty Difficulty { get; set; }
    public List<string> Tags { get; set; } = [];
    public required int TimeLimitMs { get; set; } // per test case
    public required int MemoryLimitMb { get; set; }

    /// <summary>
    /// Language id ("csharp", "python") to the skeleton the editor opens with: the stdin parsing and
    /// the call into the user's method already written, so a solver writes the algorithm and not the
    /// I/O boilerplate. Authored under content/questions/&lt;slug&gt;/starters/, empty when a question
    /// has none authored yet. Keys are not validated against Submissions:SupportedLanguages -- that
    /// list has one owner, and a key nothing offers is simply never looked up.
    /// </summary>
    public Dictionary<string, string> Starters { get; set; } = [];

    public List<TestCase> TestCases { get; set; } = [];
}

public sealed class TestCase
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public required int Ordinal { get; set; } // 1-based, unique per question -- display and run order
    public required string Input { get; set; } // fed to the program's stdin
    public required string ExpectedOutput { get; set; } // compared against its stdout
    public bool IsHidden { get; set; } // hidden test cases not shown to the user; visible ones are the samples
}

public sealed class Submission
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    /// <summary>
    /// The local user this submission belongs to -- not the provider's subject, so switching
    /// identity provider cannot orphan anyone's history (decision D3).
    /// </summary>
    public Guid OwnerUserId { get; set; }
    public required string Language { get; set; } // "csharp" | "python" for v1
    public required string SourceCode { get; set; }
    public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;
    public SubmissionVerdict? Verdict { get; set; } // set exactly when Status is Completed
    public DateTimeOffset SubmittedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }

    /// <summary>Compiler output when the submission didn't compile; null otherwise.</summary>
    public string? CompileOutput { get; set; }

    public List<SubmissionTestResult> TestResults { get; set; } = [];
}

/// <summary>
/// What one test case did for one submission. Written by the Judge's result consumer, never by a
/// request handler.
/// </summary>
public sealed class SubmissionTestResult
{
    /// <summary>
    /// Output is captured from submitted code, so it is capped in the database as well as in the
    /// sandbox: a program that prints a gigabyte must not turn one row into one.
    /// </summary>
    public const int MaxOutputLength = 4000;

    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }

    /// <summary>The test case this is the outcome of. Not a foreign key: a test case can be
    /// removed from the content while old submissions still reference what it did.</summary>
    public Guid TestCaseId { get; set; }

    public required int Ordinal { get; set; }
    public required bool Passed { get; set; }

    /// <summary>What the program printed. Never returned to a user for a hidden test case.</summary>
    public string? ActualOutput { get; set; }

    public string? ErrorMessage { get; set; }
    public long ExecutionTimeMs { get; set; }
}
