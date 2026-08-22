using DsaPractice.Api.Exceptions;
using DsaPractice.Api.Services;
using FluentValidation;

namespace DsaPractice.Api.Endpoints;

public static class SubmissionsEndpoints
{
    public static RouteGroupBuilder MapSubmissionsEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreateSubmission);
        group.MapGet("/{id:guid}", GetSubmissionById);

        return group;
    }

    private static async Task<IResult> CreateSubmission(
        CreateSubmissionRequest request,
        IValidator<CreateSubmissionRequest> validator,
        ISubmissionsService submissionsService,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new BadRequestException(
                "Submission request failed validation.",
                validationResult.Errors.Select(e => new { field = e.PropertyName, error = e.ErrorMessage }));
        }

        var response = await submissionsService.CreateSubmissionAsync(request, cancellationToken);

        return Results.Created($"/api/v1/submissions/{response.Id}", response);
    }

    private static async Task<IResult> GetSubmissionById(Guid id, ISubmissionsService submissionsService, CancellationToken cancellationToken)
    {
        var response = await submissionsService.GetSubmissionByIdAsync(id, cancellationToken);
        return Results.Ok(response);
    }
}
