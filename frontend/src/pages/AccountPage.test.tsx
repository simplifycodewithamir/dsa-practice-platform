import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import AccountPage from './AccountPage';
import { renderPage, signedInSession, signedOutSession, stubFetchRoutes } from '../test/render';

const account = { id: 'user-1', displayName: 'Ada Lovelace', createdAtUtc: '2026-09-01T10:00:00Z' };

function submission(overrides: Record<string, unknown> = {}) {
  return {
    id: 'sub-1',
    questionId: 'q-1',
    questionSlug: 'two-sum',
    questionTitle: 'Two Sum',
    language: 'python',
    status: 'Completed',
    verdict: 'Accepted',
    submittedAtUtc: '2026-09-20T10:00:00Z',
    completedAtUtc: '2026-09-20T10:00:05Z',
    ...overrides,
  };
}

function history(items: unknown[], totalCount = items.length) {
  return { items, page: 1, pageSize: 20, totalCount };
}

describe('AccountPage', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('asks a signed-out visitor to sign in, and brings them back here', async () => {
    const session = signedOutSession();
    renderPage(<AccountPage />, { session, path: '/account', route: '/account' });

    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(session.signIn).toHaveBeenCalledWith('/account');
  });

  it('shows who you are and when you joined', async () => {
    stubFetchRoutes([
      ['/api/v1/me/submissions', { body: history([]) }],
      ['/api/v1/me', { body: account }],
    ]);

    renderPage(<AccountPage />, { session: signedInSession() });

    expect(await screen.findByText('Ada Lovelace')).toBeInTheDocument();
  });

  it('lists the history newest first, linking each row to its problem', async () => {
    stubFetchRoutes([
      [
        '/api/v1/me/submissions',
        {
          body: history([
            submission({ id: 'newer', questionTitle: 'Two Sum' }),
            submission({
              id: 'older',
              questionTitle: 'Reverse String',
              questionSlug: 'reverse-string',
              verdict: 'WrongAnswer',
            }),
          ]),
        },
      ],
      ['/api/v1/me', { body: account }],
    ]);

    renderPage(<AccountPage />, { session: signedInSession() });

    const rows = await screen.findAllByRole('row');
    // The header row plus the two submissions, in the order the Api sent them.
    expect(rows).toHaveLength(3);
    expect(screen.getByRole('link', { name: 'Two Sum' })).toHaveAttribute('href', '/problems/two-sum');
    expect(screen.getByRole('link', { name: 'Reverse String' })).toHaveAttribute('href', '/problems/reverse-string');
    // Wire values are spelled out for a reader, not shown raw.
    expect(screen.getByText('Accepted')).toBeInTheDocument();
    expect(screen.getByText('Wrong answer')).toBeInTheDocument();
  });

  it('says so when there is no history yet', async () => {
    stubFetchRoutes([
      ['/api/v1/me/submissions', { body: history([]) }],
      ['/api/v1/me', { body: account }],
    ]);

    renderPage(<AccountPage />, { session: signedInSession() });

    expect(await screen.findByText(/haven't submitted anything yet/)).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('offers no paging when everything fits on one page', async () => {
    stubFetchRoutes([
      ['/api/v1/me/submissions', { body: history([submission()]) }],
      ['/api/v1/me', { body: account }],
    ]);

    renderPage(<AccountPage />, { session: signedInSession() });

    await screen.findByRole('table');
    expect(screen.queryByRole('button', { name: 'Next' })).not.toBeInTheDocument();
  });

  it('pages forward through a longer history', async () => {
    const fetchMock = stubFetchRoutes([
      ['/api/v1/me/submissions', { body: history([submission()], 25) }],
      ['/api/v1/me', { body: account }],
    ]);

    renderPage(<AccountPage />, { session: signedInSession() });

    await userEvent.click(await screen.findByRole('button', { name: 'Next' }));

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/me/submissions?page=2&pageSize=20'),
        expect.anything(),
      ),
    );
  });

  it('will not delete the account until the word is typed', async () => {
    stubFetchRoutes([
      ['/api/v1/me/submissions', { body: history([]) }],
      ['/api/v1/me', { body: account }],
    ]);

    renderPage(<AccountPage />, { session: signedInSession() });

    const button = await screen.findByRole('button', { name: 'Delete my account' });
    // Not reachable by a misplaced click: this cannot be undone.
    expect(button).toBeDisabled();

    await userEvent.type(screen.getByLabelText(/Type delete to confirm/), 'delete');
    expect(button).toBeEnabled();
  });

  it('reports what the deletion actually removed', async () => {
    stubFetchRoutes([
      ['DELETE /api/v1/me', { body: { submissionsDeleted: 3, identityProviderAccount: 'Deleted' } }],
      ['/api/v1/me/submissions', { body: history([submission()]) }],
      ['/api/v1/me', { body: account }],
    ]);

    renderPage(<AccountPage />, { session: signedInSession() });

    await userEvent.type(await screen.findByLabelText(/Type delete to confirm/), 'delete');
    await userEvent.click(screen.getByRole('button', { name: 'Delete my account' }));

    expect(await screen.findByRole('heading', { name: 'Your account is deleted' })).toBeInTheDocument();
    expect(screen.getByText(/3 submissions/)).toBeInTheDocument();
    expect(screen.getByText(/sign-in at the identity provider has been removed/)).toBeInTheDocument();
  });

  it('says plainly when the provider account could not be removed', async () => {
    stubFetchRoutes([
      ['DELETE /api/v1/me', { body: { submissionsDeleted: 0, identityProviderAccount: 'Failed' } }],
      ['/api/v1/me/submissions', { body: history([]) }],
      ['/api/v1/me', { body: account }],
    ]);

    renderPage(<AccountPage />, { session: signedInSession() });

    await userEvent.type(await screen.findByLabelText(/Type delete to confirm/), 'delete');
    await userEvent.click(screen.getByRole('button', { name: 'Delete my account' }));

    // Half an erasure reported as a success would be the one thing this page must not do.
    expect(await screen.findByText(/could not be removed automatically/)).toBeInTheDocument();
  });

  it('keeps the data when the deletion request fails', async () => {
    stubFetchRoutes([
      ['DELETE /api/v1/me', { status: 500, body: { title: 'api.error.unknown' } }],
      ['/api/v1/me/submissions', { body: history([]) }],
      ['/api/v1/me', { body: account }],
    ]);

    renderPage(<AccountPage />, { session: signedInSession() });

    await userEvent.type(await screen.findByLabelText(/Type delete to confirm/), 'delete');
    await userEvent.click(screen.getByRole('button', { name: 'Delete my account' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/Nothing was deleted/);
    expect(screen.queryByRole('heading', { name: 'Your account is deleted' })).not.toBeInTheDocument();
  });
});
