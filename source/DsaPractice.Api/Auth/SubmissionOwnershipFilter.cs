using DsaPractice.Api.Exceptions;
using DsaPractice.Api.Services;

namespace DsaPractice.Api.Auth;

/// <summary>
/// "Owner or Admin" isn't expressible as a plain declarative <see cref="Microsoft.AspNetCore.Authorization.AuthorizationPolicy"/>
/// -- checking ownership needs the resource loaded from the DB first, not just the caller's own
/// claims. A resource-based <see cref="Microsoft.AspNetCore.Authorization.IAuthorizationHandler"/>
/// could do this too, but for one endpoint an <see cref="IEndpointFilter"/> is the simpler,
/// idiomatic Minimal API mechanism -- runs after routing/model-binding, has the route's id and
/// the authenticated ClaimsPrincipal (RequireAuthorization() on the endpoint has already gated
/// entry, so User is guaranteed authenticated here) in one place.
/// </summary>
internal sealed class SubmissionOwnershipFilter(ISubmissionsService submissionsService) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var id = context.GetArgument<Guid>(0);
        var submission = await submissionsService.GetSubmissionByIdAsync(id, context.HttpContext.RequestAborted);

        var callerUserId = context.HttpContext.User.Identity?.Name;
        var isOwner = string.Equals(callerUserId, submission.UserId, StringComparison.Ordinal);
        var isAdmin = context.HttpContext.User.IsInRole(AppRoles.Admin);

        if (!isOwner && !isAdmin)
        {
            throw new ForbiddenException($"You are not authorized to view submission '{id}'.");
        }

        return await next(context);
    }
}
