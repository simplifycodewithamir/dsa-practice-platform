using DsaPractice.DataAccess.Enums;
using DsaPractice.DataAccess.Entities;

namespace DsaPractice.Api.Endpoints;

internal sealed record QuestionSummaryResponse(
    Guid Id,
    string Slug,
    string Title,
    QuestionDifficulty Difficulty,
    IReadOnlyList<string> Tags);

internal sealed record TestCaseResponse(Guid Id, int Ordinal, string Input, string ExpectedOutput);

internal sealed record QuestionDetailResponse(
    Guid Id,
    string Slug,
    string Title,
    string Description,
    QuestionDifficulty Difficulty,
    IReadOnlyList<string> Tags,
    int TimeLimitMs,
    int MemoryLimitMb,
    IReadOnlyList<TestCaseResponse> SampleTestCases)
{
    // Maps whatever TestCases were loaded -- the caller decides which ones (never hidden ones).
    public static QuestionDetailResponse FromEntity(Question question) => new(
        question.Id,
        question.Slug,
        question.Title,
        question.Description,
        question.Difficulty,
        question.Tags,
        question.TimeLimitMs,
        question.MemoryLimitMb,
        question.TestCases.Select(tc => new TestCaseResponse(tc.Id, tc.Ordinal, tc.Input, tc.ExpectedOutput)).ToList());
}
