using DsaPractice.Api.DataAccess;
using DsaPractice.Api.Endpoints;
using DsaPractice.Api.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace DsaPractice.Api.Services;

internal interface IQuestionsService
{
    Task<IReadOnlyList<QuestionSummaryResponse>> GetQuestionsAsync(CancellationToken cancellationToken);

    Task<QuestionDetailResponse> GetQuestionByIdAsync(Guid id, CancellationToken cancellationToken);
}

internal sealed class QuestionsService(DsaPracticeDbContext db) : IQuestionsService
{
    public async Task<IReadOnlyList<QuestionSummaryResponse>> GetQuestionsAsync(CancellationToken cancellationToken)
    {
        return await db.Questions
            .AsNoTracking()
            .OrderBy(q => q.Title)
            .Select(q => new QuestionSummaryResponse(q.Id, q.Title, q.Difficulty))
            .ToListAsync(cancellationToken);
    }

    public async Task<QuestionDetailResponse> GetQuestionByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        // Business rule: never surface hidden test cases through this read path.
        var question = await db.Questions
            .AsNoTracking()
            .Include(q => q.TestCases.Where(tc => !tc.IsHidden))
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Question '{id}' was not found.");

        return QuestionDetailResponse.FromEntity(question);
    }
}
