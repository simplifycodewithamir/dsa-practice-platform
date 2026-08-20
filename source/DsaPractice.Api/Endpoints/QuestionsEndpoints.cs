namespace DsaPractice.Api.Endpoints;

public static class QuestionsEndpoints
{
    public static RouteGroupBuilder MapQuestionsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetQuestions);
        group.MapGet("/{id:guid}", GetQuestionById);

        return group;
    }

    // TODO: inject IQuestionsRepository (or DbContext directly, per project convention), CancellationToken
    private static IResult GetQuestions()
    {
        // TODO: AsNoTracking() read, pagination
        return Results.Ok(Array.Empty<object>());
    }

    private static IResult GetQuestionById(Guid id)
    {
        // TODO: throw NotFoundException if missing -> global handler maps to ProblemDetails
        return Results.Ok();
    }
}
