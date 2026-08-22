using DsaPractice.Api.DataAccess.Entities;

namespace DsaPractice.Api.Endpoints;

public sealed record QuestionSummaryResponse(Guid Id, string Title, string Difficulty);

public sealed record TestCaseResponse(Guid Id, string Input, string ExpectedOutput);

public sealed record QuestionDetailResponse(
    Guid Id,
    string Title,
    string Description,
    string Difficulty,
    IReadOnlyList<TestCaseResponse> TestCases)
{
    public static QuestionDetailResponse FromEntity(Question question) => new(
        question.Id,
        question.Title,
        question.Description,
        question.Difficulty,
        question.TestCases.Select(tc => new TestCaseResponse(tc.Id, tc.Input, tc.ExpectedOutput)).ToList());
}
