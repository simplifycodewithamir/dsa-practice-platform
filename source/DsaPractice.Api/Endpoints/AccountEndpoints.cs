using DsaPractice.Api.Exceptions;
using DsaPractice.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DsaPractice.Api.Endpoints;

/// <summary>
/// "My account" -- everything scoped to the caller themselves (item 21).
///
/// There is no <c>/users/{id}</c>: every route here means "me", so one person's data cannot be
/// addressed by another, and there is no id to guess. The whole group requires a signed-in caller,
/// unconditionally -- unlike submissions, which fall back to a shared local user when
/// <c>Auth:RequireAuthentication</c> is off. "Me" with nobody signed in is not a question with a
/// defensible answer, and an unauthenticated DELETE that erased that shared row would be a footgun
/// in exactly the configuration where it is least expected.
/// </summary>
internal static class AccountEndpoints
{
    public static RouteGroupBuilder MapAccountEndpoints(this RouteGroupBuilder group)
    {
        group.RequireAuthorization();

        group.MapGet("/", GetAccount);
        group.MapGet("/submissions", GetSubmissions)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        group.MapDelete("/", DeleteAccount);

        return group;
    }

    private static async Task<Ok<AccountResponse>> GetAccount(
        IAccountService accountService, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await accountService.GetAccountAsync(cancellationToken));
    }

    private static async Task<Ok<AccountSubmissionsResponse>> GetSubmissions(
        IAccountService accountService,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = AccountSubmissionsResponse.DefaultPageSize)
    {
        // Guard clauses rather than a FluentValidation validator: two integers from the query string
        // do not earn a validator class and a DI registration. Rejected rather than clamped --
        // silently serving page 1 to a client that asked for page 0 hides the client's bug.
        if (page < 1)
        {
            throw new BadRequestException($"Page must be 1 or greater, but was {page}.");
        }

        if (pageSize is < 1 || pageSize > AccountSubmissionsResponse.MaxPageSize)
        {
            throw new BadRequestException(
                $"Page size must be between 1 and {AccountSubmissionsResponse.MaxPageSize}, but was {pageSize}.");
        }

        return TypedResults.Ok(await accountService.GetSubmissionsAsync(page, pageSize, cancellationToken));
    }

    /// <summary>
    /// 200 with a body, not 204: erasure spans this database and the identity provider, and the
    /// response says what each of them actually did. See <see cref="DeleteAccountResponse"/>.
    /// </summary>
    private static async Task<Ok<DeleteAccountResponse>> DeleteAccount(
        IAccountService accountService, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await accountService.DeleteAccountAsync(cancellationToken));
    }
}
