import { useState } from 'react';
import { Link } from 'react-router';
import { useAccount, useDeleteAccount, useMySubmissions, historyPageSize } from '../api/queries';
import { useSession } from '../auth/session';
import type { AccountSubmission, DeleteAccountResult } from '../api/client';
import { verdictLabels, type KnownVerdict } from '../components/verdictLabels';

/**
 * My account (item 21): what this site holds about me, and the button that removes it.
 *
 * The route only exists when an identity provider is configured, so the signed-out case here is a
 * visitor who has not signed in yet -- not a checkout with no provider at all.
 */
export default function AccountPage() {
  const session = useSession();
  const [page, setPage] = useState(1);
  const account = useAccount();
  const history = useMySubmissions(page);
  const deletion = useDeleteAccount();

  if (!session.isAuthenticated) {
    return (
      <section className="rounded-lg border border-slate-200 bg-white p-6">
        <h1 className="text-xl font-semibold">Your account</h1>
        <p className="mt-2 text-slate-600">Sign in to see your submission history.</p>
        <button
          type="button"
          onClick={() => session.signIn('/account')}
          className="mt-4 rounded-md bg-slate-900 px-3 py-1.5 text-sm font-medium text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-700"
        >
          Sign in
        </button>
      </section>
    );
  }

  // Once the account is gone, nothing else on this page has anything to show, and re-reading it
  // would provision a new one -- so this replaces the page rather than sitting alongside it.
  if (deletion.isSuccess) {
    return <DeletedPanel result={deletion.data} onSignOut={session.signOut} />;
  }

  return (
    <div className="space-y-8">
      <section>
        <h1 className="text-2xl font-semibold tracking-tight">Your account</h1>
        {account.isPending && <p className="mt-2 text-slate-500">Loading your account…</p>}
        {account.isError && (
          <p role="alert" className="mt-2 text-rose-700">
            Your account could not be loaded. Reload the page to try again.
          </p>
        )}
        {account.data && (
          <dl className="mt-3 grid gap-x-8 gap-y-2 text-sm sm:grid-cols-[auto_1fr]">
            <dt className="text-slate-500">Signed in as</dt>
            <dd className="font-medium">{account.data.displayName ?? 'No name from your provider'}</dd>
            <dt className="text-slate-500">Joined</dt>
            <dd>{formatDate(account.data.createdAtUtc)}</dd>
          </dl>
        )}
      </section>

      <SubmissionHistory
        page={page}
        onPageChange={setPage}
        items={history.data?.items}
        // Coerced here: the Api's OpenAPI document types every integer as `integer | string`, so the
        // generated client widens each one and arithmetic on it has to be made explicit.
        totalCount={Number(history.data?.totalCount ?? 0)}
        isPending={history.isPending}
        isError={history.isError}
      />

      <DangerZone
        onDelete={() => deletion.mutate()}
        isDeleting={deletion.isPending}
        hasFailed={deletion.isError}
      />
    </div>
  );
}

function SubmissionHistory({
  page,
  onPageChange,
  items,
  totalCount,
  isPending,
  isError,
}: {
  page: number;
  onPageChange: (page: number) => void;
  items?: AccountSubmission[];
  totalCount: number;
  isPending: boolean;
  isError: boolean;
}) {
  const lastPage = Math.max(1, Math.ceil(totalCount / historyPageSize));

  return (
    <section>
      <h2 className="text-lg font-semibold">Submission history</h2>

      {isPending && <p className="mt-2 text-slate-500">Loading your submissions…</p>}
      {isError && (
        <p role="alert" className="mt-2 text-rose-700">
          Your submissions could not be loaded. Reload the page to try again.
        </p>
      )}

      {items?.length === 0 && (
        <p className="mt-2 text-slate-600">
          You haven&apos;t submitted anything yet. <Link to="/" className="underline">Pick a problem</Link>.
        </p>
      )}

      {items !== undefined && items.length > 0 && (
        <>
          {/* A table, because this is tabular data: a screen reader announces the column a cell
              belongs to, which a list of divs cannot do. */}
          <div className="mt-3 overflow-x-auto">
            <table className="w-full min-w-[36rem] border-collapse text-sm">
              <caption className="sr-only">
                Your submissions, newest first. Page {page} of {lastPage}.
              </caption>
              <thead>
                <tr className="border-b border-slate-200 text-left text-slate-500">
                  <th scope="col" className="py-2 pr-4 font-medium">Problem</th>
                  <th scope="col" className="py-2 pr-4 font-medium">Language</th>
                  <th scope="col" className="py-2 pr-4 font-medium">Result</th>
                  <th scope="col" className="py-2 font-medium">Submitted</th>
                </tr>
              </thead>
              <tbody>
                {items.map((submission) => (
                  <tr key={submission.id} className="border-b border-slate-100">
                    <td className="py-2 pr-4">
                      <Link
                        to={`/problems/${submission.questionSlug}`}
                        className="font-medium underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-700"
                      >
                        {submission.questionTitle}
                      </Link>
                    </td>
                    <td className="py-2 pr-4 text-slate-600">{submission.language}</td>
                    <td className="py-2 pr-4">{describeResult(submission)}</td>
                    <td className="py-2 text-slate-600">{formatDate(submission.submittedAtUtc)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {totalCount > historyPageSize && (
            <nav aria-label="Submission history pages" className="mt-4 flex items-center gap-3 text-sm">
              <button
                type="button"
                disabled={page <= 1}
                onClick={() => onPageChange(page - 1)}
                className="rounded-md border border-slate-300 px-3 py-1.5 font-medium text-slate-700 disabled:cursor-not-allowed disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-700"
              >
                Previous
              </button>
              <span className="text-slate-600">
                Page {page} of {lastPage}
              </span>
              <button
                type="button"
                disabled={page >= lastPage}
                onClick={() => onPageChange(page + 1)}
                className="rounded-md border border-slate-300 px-3 py-1.5 font-medium text-slate-700 disabled:cursor-not-allowed disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-700"
              >
                Next
              </button>
            </nav>
          )}
        </>
      )}
    </section>
  );
}

/**
 * Deleting the account.
 *
 * Typing the word is the confirmation rather than a dialog: this cannot be undone, and a misplaced
 * click should not be enough to trigger it. The input has a real label, and the button stays
 * disabled until the word matches.
 */
function DangerZone({
  onDelete,
  isDeleting,
  hasFailed,
}: {
  onDelete: () => void;
  isDeleting: boolean;
  hasFailed: boolean;
}) {
  const [confirmation, setConfirmation] = useState('');
  const confirmed = confirmation.trim().toLowerCase() === 'delete';

  return (
    <section className="rounded-lg border border-rose-200 bg-rose-50/50 p-4">
      <h2 className="text-lg font-semibold text-rose-900">Delete your account</h2>
      <p className="mt-1 text-sm text-rose-900/80">
        This removes your submissions and everything you have written here, and deletes your sign-in at
        the identity provider. It cannot be undone.
      </p>

      <form
        className="mt-4 flex flex-wrap items-end gap-3"
        onSubmit={(event) => {
          event.preventDefault();
          if (confirmed) {
            onDelete();
          }
        }}
      >
        <div>
          <label htmlFor="delete-confirmation" className="block text-sm font-medium text-rose-900">
            Type <span className="font-mono">delete</span> to confirm
          </label>
          <input
            id="delete-confirmation"
            name="delete-confirmation"
            type="text"
            autoComplete="off"
            value={confirmation}
            onChange={(event) => setConfirmation(event.target.value)}
            className="mt-1 rounded-md border border-rose-300 bg-white px-3 py-1.5 text-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-rose-700"
          />
        </div>
        <button
          type="submit"
          disabled={!confirmed || isDeleting}
          className="rounded-md bg-rose-700 px-3 py-1.5 text-sm font-medium text-white disabled:cursor-not-allowed disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-rose-900"
        >
          {isDeleting ? 'Deleting…' : 'Delete my account'}
        </button>
      </form>

      {hasFailed && (
        <p role="alert" className="mt-3 text-sm text-rose-800">
          Nothing was deleted — the request failed. Try again, and if it keeps failing your data is
          still here.
        </p>
      )}
    </section>
  );
}

/** What erasure actually managed to do. Says so plainly, including when it was only half done. */
function DeletedPanel({ result, onSignOut }: { result: DeleteAccountResult; onSignOut: () => void }) {
  const providerRemoved = result.identityProviderAccount === 'Deleted';
  // See the note on totalCount above: the generated type is `number | string`.
  const submissionsDeleted = Number(result.submissionsDeleted);

  return (
    <section className="rounded-lg border border-slate-200 bg-white p-6">
      <h1 className="text-xl font-semibold">Your account is deleted</h1>
      <p role="status" className="mt-2 text-slate-700">
        {submissionsDeleted === 1
          ? '1 submission and everything stored with it has been removed.'
          : `${submissionsDeleted} submissions and everything stored with them have been removed.`}
      </p>

      {providerRemoved ? (
        <p className="mt-2 text-slate-700">Your sign-in at the identity provider has been removed too.</p>
      ) : (
        // Not hidden behind a success message: the person asked for erasure and is entitled to know
        // that one half of it did not happen.
        <p className="mt-2 text-amber-900">
          Your sign-in at the identity provider could not be removed automatically, so signing in again
          would create a new, empty account. It has been logged so it can be removed by hand.
        </p>
      )}

      <button
        type="button"
        onClick={onSignOut}
        className="mt-4 rounded-md bg-slate-900 px-3 py-1.5 text-sm font-medium text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-700"
      >
        Sign out
      </button>
    </section>
  );
}

function describeResult(submission: AccountSubmission) {
  if (submission.status !== 'Completed') {
    return <span className="text-slate-500">Judging…</span>;
  }

  const accepted = submission.verdict === 'Accepted';
  return (
    <span className={accepted ? 'font-medium text-emerald-700' : 'text-rose-700'}>
      {verdictLabels[submission.verdict as KnownVerdict] ?? submission.verdict}
    </span>
  );
}

function formatDate(value: string | undefined) {
  if (value === undefined) {
    return '';
  }

  return new Date(value).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}
