namespace DsaPractice.Api.DataAccess.Entities;

public sealed class Question
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Difficulty { get; set; } // Easy | Medium | Hard — TODO: consider enum
    public List<TestCase> TestCases { get; set; } = [];
}

public sealed class TestCase
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public required string Input { get; set; }
    public required string ExpectedOutput { get; set; }
    public bool IsHidden { get; set; } // hidden test cases not shown to the user
}

public sealed class Submission
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public required string UserId { get; set; }
    public required string Language { get; set; } // "csharp" | "python" for v1
    public required string SourceCode { get; set; }
    public string Status { get; set; } = "Pending"; // Pending | Running | Passed | Failed | Error
    public DateTimeOffset SubmittedAtUtc { get; set; }
}
