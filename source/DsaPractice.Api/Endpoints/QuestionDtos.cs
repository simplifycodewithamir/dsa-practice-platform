using DsaPractice.Api.DataAccess.Entities;

namespace DsaPractice.Api.Endpoints;

internal sealed record QuestionSummaryResponse(Guid Id, string Title, string Difficulty);

internal sealed record TestCaseResponse(Guid Id, string Input, string ExpectedOutput);

internal sealed record QuestionDetailResponse(
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
