import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import type { ReactElement } from 'react';
import { vi } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router';
import { SessionContext, type Session } from '../auth/session';

/**
 * A session with nothing configured: the default for tests that are not about signing in, and the
 * same shape a checkout with no identity provider runs with.
 */
export const noAuthSession: Session = {
  mode: 'disabled',
  isAuthenticated: false,
  isLoading: false,
  signIn: () => {},
  signOut: () => {},
};

/** A signed-in session, with `signIn`/`signOut` as spies so a test can assert what was called. */
export function signedInSession(overrides: Partial<Session> = {}): Session {
  return {
    mode: 'oidc',
    isAuthenticated: true,
    isLoading: false,
    displayName: 'Ada Lovelace',
    signIn: vi.fn(),
    signOut: vi.fn(),
    ...overrides,
  };
}

/** A session with a provider configured that nobody has signed in to. */
export function signedOutSession(overrides: Partial<Session> = {}): Session {
  return signedInSession({ isAuthenticated: false, displayName: undefined, ...overrides });
}

/**
 * Renders a page the way the app does -- inside a query client, a router and a session -- with
 * retries off so a test asserting an error state doesn't wait for retry backoff.
 */
export function renderPage(
  element: ReactElement,
  { path = '/', route = '/', session = noAuthSession }: { path?: string; route?: string; session?: Session } = {},
) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <SessionContext.Provider value={session}>
          <Routes>
            <Route path={route} element={element} />
          </Routes>
        </SessionContext.Provider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

/** Stubs fetch with one response per call, in order. */
export function stubFetchJson(...responses: Array<{ status?: number; body: unknown }>) {
  let call = 0;

  const fetchMock = vi.fn().mockImplementation(() => {
    const response = responses[Math.min(call++, responses.length - 1)];
    const status = response.status ?? 200;

    return Promise.resolve({
      ok: status < 400,
      status,
      json: async () => response.body,
    });
  });

  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}
