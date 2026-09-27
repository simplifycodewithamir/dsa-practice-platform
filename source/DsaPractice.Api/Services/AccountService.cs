using DsaPractice.Api.Auth;
using DsaPractice.Api.Endpoints;
using DsaPractice.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace DsaPractice.Api.Services;

internal interface IAccountService
{
    Task<AccountResponse> GetAccountAsync(CancellationToken cancellationToken);

    Task<AccountSubmissionsResponse> GetSubmissionsAsync(int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// Erases the caller: their submissions, the results and queued judge requests carrying their
    /// code, their user row, and their account at the identity provider.
    /// </summary>
    Task<DeleteAccountResponse> DeleteAccountAsync(CancellationToken cancellationToken);
}

internal sealed class AccountService(
    DsaPracticeDbContext db,
    ICurrentUserProvider currentUserProvider,
    IIdentityProviderAccounts identityProviderAccounts,
    ILogger<AccountService> logger) : IAccountService
{
    public async Task<AccountResponse> GetAccountAsync(CancellationToken cancellationToken)
    {
        // Provisioned on first sight like everywhere else (decision D3), so someone who has signed
        // in but never submitted sees their account rather than a 404 they can do nothing about.
        var user = await currentUserProvider.GetOrCreateAsync(cancellationToken);

        return new AccountResponse(user.Id, user.DisplayName, user.CreatedAtUtc);
    }

    public async Task<AccountSubmissionsResponse> GetSubmissionsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var user = await currentUserProvider.GetOrCreateAsync(cancellationToken);

        var mine = db.Submissions.AsNoTracking().Where(s => s.OwnerUserId == user.Id);

        // Counted separately from the page: the total is what tells the UI whether there is a next
        // page at all, and it cannot be inferred from a page that happens to come back full.
        var totalCount = await mine.CountAsync(cancellationToken);

        // Joined to Questions rather than looked up per row -- one query, no N+1. Ordered by id as
        // well as time: two submissions can share a timestamp, and an unstable order would let a
        // row appear on two pages or on neither.
        var items = await (from submission in mine
                           join question in db.Questions.AsNoTracking() on submission.QuestionId equals question.Id
                           orderby submission.SubmittedAtUtc descending, submission.Id
                           select new AccountSubmissionResponse(
                               submission.Id,
                               question.Id,
                               question.Slug,
                               question.Title,
                               submission.Language,
                               submission.Status,
                               submission.Verdict,
                               submission.SubmittedAtUtc,
                               submission.CompletedAtUtc))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new AccountSubmissionsResponse(items, page, pageSize, totalCount);
    }

    public async Task<DeleteAccountResponse> DeleteAccountAsync(CancellationToken cancellationToken)
    {
        // A caller who has never been seen before gets provisioned and then immediately erased. That
        // is a wasted insert, and it is still the right shape: identity resolves exactly one way in
        // this application, and a second path that can only be reached by deleting an account that
        // does not exist would be the less trustworthy of the two.
        var user = await currentUserProvider.GetOrCreateAsync(cancellationToken);
        var (issuer, subject) = (user.Issuer, user.Subject);

        var submissionIds = await db.Submissions
            .AsNoTracking()
            .Where(s => s.OwnerUserId == user.Id)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        // The outbox stores a judge request's payload, which contains the source code, and keys it
        // by the submission id as a string.
        var messageIds = submissionIds.ConvertAll(id => id.ToString());

        // Each ExecuteDelete is its own statement, so without this the database could be left with
        // submissions whose owner is gone -- or worse, an owner gone and the code still queued.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.OutboxMessages
            .Where(m => messageIds.Contains(m.MessageId))
            .ExecuteDeleteAsync(cancellationToken);

        // Explicit, rather than leaning on the ON DELETE CASCADE that SubmissionTestResultConfiguration
        // declares. Erasure is the one operation that must not quietly depend on a delete rule
        // written in another file, where changing it would look like a modelling decision.
        await db.SubmissionTestResults
            .Where(r => submissionIds.Contains(r.SubmissionId))
            .ExecuteDeleteAsync(cancellationToken);

        var submissionsDeleted = await db.Submissions
            .Where(s => s.OwnerUserId == user.Id)
            .ExecuteDeleteAsync(cancellationToken);

        // Last: the user row is what the submissions' foreign key restricts against, so it cannot
        // go first, and leaving it behind would leave an identifier with nothing attached to it.
        await db.Users
            .Where(u => u.Id == user.Id)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Erased a user from {Issuer}: {SubmissionCount} submissions and everything queued for them.",
            issuer, submissionsDeleted);

        // Not the request's token: the local data is already committed as deleted, so abandoning
        // this because the browser went away would leave the provider's copy alive with nothing left
        // in the product able to ask for it again. The call is bounded by the HTTP client's own
        // timeout instead (see the resilience handler in Program.cs).
        var providerOutcome = await identityProviderAccounts.DeleteUserAsync(issuer, subject, CancellationToken.None);

        return new DeleteAccountResponse(submissionsDeleted, providerOutcome);
    }
}
