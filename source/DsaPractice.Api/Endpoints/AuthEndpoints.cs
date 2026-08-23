using DsaPractice.Api.Auth;
using DsaPractice.Api.Exceptions;
using FluentValidation;

namespace DsaPractice.Api.Endpoints;

/// <summary>
/// Development-only. Mapped in Program.cs only inside `if (app.Environment.IsDevelopment())` --
/// see the "auth architecture" note on <see cref="AppRoles"/> for why this exists instead of a
/// real OAuth2/OIDC Authority.
/// </summary>
internal static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/dev-token", IssueDevToken);

        return group;
    }

    private static async Task<IResult> IssueDevToken(
        CreateDevTokenRequest request,
        IValidator<CreateDevTokenRequest> validator,
        IJwtTokenService tokenService,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new BadRequestException(
                "Dev token request failed validation.",
                validationResult.Errors.Select(e => new { field = e.PropertyName, error = e.ErrorMessage }));
        }

        var role = request.Role ?? AppRoles.User;
        var accessToken = tokenService.CreateToken(request.UserId, role);

        return Results.Ok(new DevTokenResponse(accessToken, "Bearer", ExpiresInSeconds: 3600));
    }
}
