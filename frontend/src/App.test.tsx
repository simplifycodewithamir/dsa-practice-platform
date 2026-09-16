import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import App from './App';
import { stubFetchJson } from './test/render';

function renderAt(path: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('routing', () => {
  // The pages fetch as soon as they mount, so every route needs something to answer with.
  beforeEach(() => {
    stubFetchJson({ body: [{ id: '1', slug: 'two-sum', title: 'Two Sum', difficulty: 'Easy', tags: [] }] });
  });

  afterEach(() => vi.unstubAllGlobals());

  it('shows the questions page at the root', async () => {
    renderAt('/');

    expect(await screen.findByRole('heading', { name: 'Questions' })).toBeInTheDocument();
  });

  it('routes a problem slug to the question page', async () => {
    stubFetchJson({ body: { id: '1', slug: 'two-sum', title: 'Two Sum', difficulty: 'Easy', tags: [], description: '', sampleTestCases: [] } });

    renderAt('/problems/two-sum');

    expect(await screen.findByRole('heading', { name: 'Two Sum' })).toBeInTheDocument();
  });

  it('shows a not-found page for an unknown route', () => {
    renderAt('/nothing-here');

    expect(screen.getByRole('heading', { name: 'Page not found' })).toBeInTheDocument();
  });

  it('keeps the site header on every page', () => {
    renderAt('/problems/two-sum');

    expect(screen.getByRole('link', { name: 'DSA Practice' })).toBeInTheDocument();
  });
});
