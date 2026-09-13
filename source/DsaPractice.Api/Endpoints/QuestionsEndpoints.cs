using DsaPractice.Api.Services;
using DsaPractice.DataAccess.Entities;
using Microsoft.AspNetCore.Http.HttpResults;

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
        // A slug that fails the route constraint 404s before this handler runs, so the
        // not-found response has two sources -- both ProblemDetails, neither inferable.
        group.MapGet($"/{SlugRouteParameter}", GetQuestionBySlug)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Ok<IReadOnlyList<QuestionSummaryResponse>>> GetQuestions(IQuestionsService questionsService, CancellationToken cancellationToken)
    {
        var questions = await questionsService.GetQuestionsAsync(cancellationToken);
        return TypedResults.Ok(questions);
    }

    private static async Task<Ok<QuestionDetailResponse>> GetQuestionBySlug(string slug, IQuestionsService questionsService, CancellationToken cancellationToken)
    {
        var question = await questionsService.GetQuestionBySlugAsync(slug, cancellationToken);
        return TypedResults.Ok(question);
    }
}
