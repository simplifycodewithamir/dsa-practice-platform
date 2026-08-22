using DsaPractice.Api.DataAccess;
using DsaPractice.Api.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace DsaPractice.Api.Endpoints;

public static class QuestionsEndpoints
{
    public static RouteGroupBuilder MapQuestionsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetQuestions);
        group.MapGet("/{id:guid}", GetQuestionById);

        return group;
    }

    private static async Task<IResult> GetQuestions(DsaPracticeDbContext db, CancellationToken cancellationToken)
    {
        var questions = await db.Questions
            .AsNoTracking()
            .OrderBy(q => q.Title)
            .Select(q => new QuestionSummaryResponse(q.Id, q.Title, q.Difficulty))
            .ToListAsync(cancellationToken);

        return Results.Ok(questions);
    }

    private static async Task<IResult> GetQuestionById(Guid id, DsaPracticeDbContext db, CancellationToken cancellationToken)
    {
        var question = await db.Questions
            .AsNoTracking()
            .Include(q => q.TestCases.Where(tc => !tc.IsHidden))
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Question '{id}' was not found.");

        return Results.Ok(QuestionDetailResponse.FromEntity(question));
    }
}
