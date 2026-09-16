using DsaPractice.Api.Exceptions;
using DsaPractice.DataAccess;
using DsaPractice.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;

namespace DsaPractice.Api.Auth;

/// <summary>
/// "You may read your own submissions; an admin may read anyone's."
///
/// An endpoint filter rather than an authorization policy because the rule needs the submission
/// loaded from the database to evaluate. A declarative policy cannot express that without a
/// resource-based handler, which is more machinery than one endpoint justifies.
///
/// It returns 404, not 403, for someone else's submission: 403 would confirm that the id exists.
/// </summary>
internal sealed class SubmissionOwnershipFilter(
    DsaPracticeDbContext db,
    ICurrentUserProvider currentUserProvider) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var submissionId = context.GetArgument<Guid>(0);
        var cancellationToken = context.HttpContext.RequestAborted;

        var ownerId = await db.Submissions
            .AsNoTracking()
            .Where(s => s.Id == submissionId)
            .Select(s => (Guid?)s.OwnerUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (ownerId is null)
        {
            throw new NotFoundException($"Submission '{submissionId}' was not found.");
        }

        var user = await currentUserProvider.GetOrCreateAsync(cancellationToken);

        if (user.Role != UserRole.Admin && ownerId != user.Id)
        {
            throw new NotFoundException($"Submission '{submissionId}' was not found.");
        }

        return await next(context);
    }
}
