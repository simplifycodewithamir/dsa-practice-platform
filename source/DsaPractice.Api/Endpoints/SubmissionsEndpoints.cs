using DsaPractice.Api.Auth;
using DsaPractice.Api.Exceptions;
using DsaPractice.Api.Services;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DsaPractice.Api.Endpoints;

internal static class SubmissionsEndpoints
{
    public static RouteGroupBuilder MapSubmissionsEndpoints(this RouteGroupBuilder group)
    {
        // The handlers' return types document the success responses; the failures are thrown as
        // ApiExceptions and turned into ProblemDetails by GlobalExceptionHandler, which the
        // OpenAPI document can't infer -- hence the explicit ProducesProblem calls.
        // Reading a submission is always owner-or-admin; when authentication is not required the
        // "owner" is the shared local user, so the rule holds either way.
        group.MapPost("/", CreateSubmission)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapGet("/{id:guid}", GetSubmissionById)
            .AddEndpointFilter<SubmissionOwnershipFilter>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Created<SubmissionResponse>> CreateSubmission(
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

        return TypedResults.Created($"/api/v1/submissions/{response.Id}", response);
    }

    private static async Task<Ok<SubmissionResponse>> GetSubmissionById(Guid id, ISubmissionsService submissionsService, CancellationToken cancellationToken)
    {
        var response = await submissionsService.GetSubmissionByIdAsync(id, cancellationToken);
        return TypedResults.Ok(response);
    }
}
