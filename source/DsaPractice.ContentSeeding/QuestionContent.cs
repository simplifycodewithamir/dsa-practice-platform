using DsaPractice.DataAccess.Enums;

namespace DsaPractice.ContentSeeding;

/// <summary>One question as authored under content/questions/&lt;slug&gt;/.</summary>
public sealed record QuestionContent(
    string Slug,
    string Title,
    QuestionDifficulty Difficulty,
    IReadOnlyList<string> Tags,
    int TimeLimitMs,
    int MemoryLimitMb,
    string Statement,
    IReadOnlyDictionary<string, string> Starters,
    IReadOnlyList<TestCaseContent> TestCases);

public sealed record TestCaseContent(int Ordinal, string Input, string ExpectedOutput, bool IsHidden);

/// <summary>What the seeder did, for the log line and for tests to assert on.</summary>
public sealed record SeedResult(int Created, int Updated, int Unchanged, int TestCasesRemoved);

/// <summary>Content on disk is malformed. The message lists every problem found, not just the first.</summary>
public sealed class ContentException(string message) : Exception(message);
