import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import App from './App';

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
  it('shows the questions page at the root', () => {
    renderAt('/');

    expect(screen.getByRole('heading', { name: 'Questions' })).toBeInTheDocument();
  });

  it('routes a problem slug to the question page', () => {
    renderAt('/problems/two-sum');

    expect(screen.getByRole('heading', { name: 'two-sum' })).toBeInTheDocument();
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
