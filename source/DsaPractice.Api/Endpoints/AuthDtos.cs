using DsaPractice.Api.Auth;
using FluentValidation;

namespace DsaPractice.Api.Endpoints;

internal sealed record CreateDevTokenRequest(string UserId, string? Role);

internal sealed record DevTokenResponse(string AccessToken, string TokenType, int ExpiresInSeconds);

internal sealed class CreateDevTokenRequestValidator : AbstractValidator<CreateDevTokenRequest>
{
    private static readonly string[] AllowedRoles = [AppRoles.User, AppRoles.Admin];

    public CreateDevTokenRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Role)
            .Must(role => AllowedRoles.Contains(role))
            .When(x => x.Role is not null)
            .WithMessage($"Role must be one of: {string.Join(", ", AllowedRoles)}.");
    }
}
