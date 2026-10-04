using DsaPractice.Api.Auth;
using DsaPractice.DataAccess.Enums;

namespace DsaPractice.Api.Endpoints;

/// <summary>
/// The account, as its owner sees it. Deliberately thin: identity lives with the provider
/// (decision D3), so there is nothing here to edit and no profile to maintain. The role is not
/// exposed -- it is an authorization decision, not something a person needs told back to them.
/// </summary>
internal sealed record AccountResponse(Guid Id, string? DisplayName, DateTimeOffset CreatedAtUtc);

/// <summary>
/// One row of my history. Carries the question's slug and title so the list can link to the
/// problem without a request per row, and deliberately omits <c>SourceCode</c>: a history page
/// does not need every submission's source, and sending it would make the payload grow with the
/// length of everything the person has ever written.
/// </summary>
internal sealed record AccountSubmissionResponse(
    Guid Id,
    Guid QuestionId,
    string QuestionSlug,
    string QuestionTitle,
    string Language,
    SubmissionStatus Status,
    SubmissionVerdict? Verdict,
    DateTimeOffset SubmittedAtUtc,
    DateTimeOffset? CompletedAtUtc);

/// <summary>
/// A page of history. A concrete type rather than a generic <c>PagedResponse&lt;T&gt;</c>: this is
/// the only paged endpoint, and a generic one would put a mangled schema name
/// (<c>PagedResponseOfAccountSubmissionResponse</c>) in the OpenAPI document and therefore in the
/// generated client, for no gain.
/// </summary>
internal sealed record AccountSubmissionsResponse(
    IReadOnlyList<AccountSubmissionResponse> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    /// <summary>Defaults and bounds for the query string, enforced at the endpoint.</summary>
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}

/// <summary>
/// What an erasure actually did.
///
/// A body rather than a bare 204, because erasure spans two systems that cannot share a transaction:
/// the local data is deleted by the time the provider is called, and if that call fails the person's
/// sign-in still exists. Answering 204 either way would mean the API claims a complete erasure it
/// has not performed -- which is the one thing a right-to-erasure endpoint must not do.
/// </summary>
internal sealed record DeleteAccountResponse(
    int SubmissionsDeleted,
    IdentityProviderDeletionOutcome IdentityProviderAccount);
