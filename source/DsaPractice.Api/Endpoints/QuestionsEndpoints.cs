using DsaPractice.DataAccess.Entities;
using DsaPractice.Api.Services;

namespace DsaPractice.Api.Endpoints;

internal static class QuestionsEndpoints
{
    // Same pattern as the DB check constraint. A malformed slug fails routing (404 via
    // UseStatusCodePages) before any handler runs or any query is issued.
    private static readonly string SlugRouteParameter =
        $"{{slug:maxlength({Question.SlugMaxLength}):regex({Question.SlugPattern})}}";

    public static RouteGroupBuilder MapQuestionsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetQuestions);
        group.MapGet($"/{SlugRouteParameter}", GetQuestionBySlug);

        return group;
    }

    private static async Task<IResult> GetQuestions(IQuestionsService questionsService, CancellationToken cancellationToken)
    {
        var questions = await questionsService.GetQuestionsAsync(cancellationToken);
        return Results.Ok(questions);
    }

    private static async Task<IResult> GetQuestionBySlug(string slug, IQuestionsService questionsService, CancellationToken cancellationToken)
    {
        var question = await questionsService.GetQuestionBySlugAsync(slug, cancellationToken);
        return Results.Ok(question);
    }
}
