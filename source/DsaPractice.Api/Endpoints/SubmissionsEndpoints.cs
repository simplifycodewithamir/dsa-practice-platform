using DsaPractice.Api.Auth;
using DsaPractice.Api.Exceptions;
using DsaPractice.Api.Services;
using FluentValidation;

namespace DsaPractice.Api.Endpoints;

internal static class SubmissionsEndpoints
{
    public static RouteGroupBuilder MapSubmissionsEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreateSubmission).RequireAuthorization();

        // RequireAuthorization() (the default policy -- must be authenticated) is the coarse,
        // declarative gate; SubmissionOwnershipFilter is the finer-grained, resource-specific
        // rule ("must own this submission, unless Admin") that a plain policy can't express
        // without loading the resource first. See the filter's own doc comment.
        group.MapGet("/{id:guid}", GetSubmissionById)
            .RequireAuthorization()
            .AddEndpointFilter<SubmissionOwnershipFilter>();

        return group;
    }

    private static async Task<IResult> CreateSubmission(
        HttpContext httpContext,
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

        // Non-null: RequireAuthorization() above already rejected unauthenticated callers before
        // this handler could run, and NameClaimType is configured to the token's "sub" claim.
        var userId = httpContext.User.Identity!.Name!;
        var response = await submissionsService.CreateSubmissionAsync(request, userId, cancellationToken);

        return Results.Created($"/api/v1/submissions/{response.Id}", response);
    }

    private static async Task<IResult> GetSubmissionById(Guid id, ISubmissionsService submissionsService, CancellationToken cancellationToken)
    {
        var response = await submissionsService.GetSubmissionByIdAsync(id, cancellationToken);
        return Results.Ok(response);
    }
}
