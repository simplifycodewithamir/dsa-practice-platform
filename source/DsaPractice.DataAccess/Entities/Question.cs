namespace DsaPractice.Api.DataAccess.Entities;

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
    public required string UserId { get; set; }
    public required string Language { get; set; } // "csharp" | "python" for v1
    public required string SourceCode { get; set; }
    public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;
    public SubmissionVerdict? Verdict { get; set; } // set exactly when Status is Completed
    public DateTimeOffset SubmittedAtUtc { get; set; }
}
