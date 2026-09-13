using DsaPractice.Api.DataAccess;
using DsaPractice.Api.Endpoints;
using DsaPractice.Api.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace DsaPractice.Api.Services;

internal interface IQuestionsService
{
    Task<IReadOnlyList<QuestionSummaryResponse>> GetQuestionsAsync(CancellationToken cancellationToken);

    Task<QuestionDetailResponse> GetQuestionBySlugAsync(string slug, CancellationToken cancellationToken);
}

internal sealed class QuestionsService(DsaPracticeDbContext db) : IQuestionsService
{
    public async Task<IReadOnlyList<QuestionSummaryResponse>> GetQuestionsAsync(CancellationToken cancellationToken)
    {
        return await db.Questions
            .AsNoTracking()
            .OrderBy(q => q.Title)
            .Select(q => new QuestionSummaryResponse(q.Id, q.Slug, q.Title, q.Difficulty, q.Tags))
            .ToListAsync(cancellationToken);
    }

    public async Task<QuestionDetailResponse> GetQuestionBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        // Business rule: never surface hidden test cases through this read path -- only the
        // samples, in the order they're presented and run.
        var question = await db.Questions
            .AsNoTracking()
            .Include(q => q.TestCases.Where(tc => !tc.IsHidden).OrderBy(tc => tc.Ordinal))
            .FirstOrDefaultAsync(q => q.Slug == slug, cancellationToken)
            ?? throw new NotFoundException($"Question '{slug}' was not found.");

        return QuestionDetailResponse.FromEntity(question);
    }
}
