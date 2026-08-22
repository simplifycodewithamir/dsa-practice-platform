using DsaPractice.Api.Services;

namespace DsaPractice.Api.Endpoints;

public static class QuestionsEndpoints
{
    public static RouteGroupBuilder MapQuestionsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetQuestions);
        group.MapGet("/{id:guid}", GetQuestionById);

        return group;
    }

    private static async Task<IResult> GetQuestions(IQuestionsService questionsService, CancellationToken cancellationToken)
    {
        var questions = await questionsService.GetQuestionsAsync(cancellationToken);
        return Results.Ok(questions);
    }

    private static async Task<IResult> GetQuestionById(Guid id, IQuestionsService questionsService, CancellationToken cancellationToken)
    {
        var question = await questionsService.GetQuestionByIdAsync(id, cancellationToken);
        return Results.Ok(question);
    }
}
