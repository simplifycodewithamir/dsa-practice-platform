import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import type { ReactElement } from 'react';
import { vi } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router';

/**
 * Renders a page the way the app does -- inside a query client and a router -- with retries off so
 * a test asserting an error state doesn't wait for retry backoff.
 */
export function renderPage(element: ReactElement, { path = '/', route = '/' } = {}) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path={route} element={element} />
        </Routes>
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
