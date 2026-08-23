using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DsaPractice.Api.Auth;
using DsaPractice.Api.Endpoints;
using DsaPractice.Api.Exceptions;
using DsaPractice.Api.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DsaPractice.Api.UnitTests.Auth;

public class SubmissionOwnershipFilterTests
{
    private static readonly Guid SubmissionId = Guid.NewGuid();
    private static readonly EndpointFilterDelegate Next = _ => ValueTask.FromResult<object?>(Results.Ok());

    [Fact]
    public async Task InvokeAsync_CallerIsOwner_CallsNext()
    {
        var sut = CreateSut(submissionOwnerId: "user-1", out var context, callerUserId: "user-1");

        var result = await sut.InvokeAsync(context, Next);

        Assert.IsAssignableFrom<IResult>(result);
    }

    [Fact]
    public async Task InvokeAsync_CallerIsAdminNotOwner_CallsNext()
    {
        var sut = CreateSut(submissionOwnerId: "user-1", out var context, callerUserId: "admin-1", callerRole: AppRoles.Admin);

        var result = await sut.InvokeAsync(context, Next);

        Assert.IsAssignableFrom<IResult>(result);
    }

    [Fact]
    public async Task InvokeAsync_CallerIsNeitherOwnerNorAdmin_ThrowsForbiddenException()
    {
        var sut = CreateSut(submissionOwnerId: "user-1", out var context, callerUserId: "someone-else");

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.InvokeAsync(context, Next).AsTask());
    }

    private static SubmissionOwnershipFilter CreateSut(
        string submissionOwnerId,
        out EndpointFilterInvocationContext context,
        string callerUserId,
        string callerRole = AppRoles.User)
    {
        var submissionsService = new StubSubmissionsService(new SubmissionResponse(
            SubmissionId, Guid.NewGuid(), submissionOwnerId, "csharp", "Pending", DateTimeOffset.UtcNow));

        var identity = new ClaimsIdentity(
            [new Claim(JwtRegisteredClaimNames.Sub, callerUserId), new Claim("role", callerRole)],
            authenticationType: "Test",
            nameType: JwtRegisteredClaimNames.Sub,
            roleType: "role");

        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        context = EndpointFilterInvocationContext.Create(httpContext, SubmissionId);

        return new SubmissionOwnershipFilter(submissionsService);
    }

    // ISubmissionsService is internal, so Moq's runtime proxy generator can't see it without
    // granting InternalsVisibleTo to Moq's dynamic assembly on the production project -- a
    // hand-written stub avoids needing that.
    private sealed class StubSubmissionsService(SubmissionResponse response) : ISubmissionsService
    {
        public Task<SubmissionResponse> CreateSubmissionAsync(CreateSubmissionRequest request, string userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by SubmissionOwnershipFilterTests.");

        public Task<SubmissionResponse> GetSubmissionByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}
