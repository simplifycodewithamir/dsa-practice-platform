import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import QuestionsPage from './QuestionsPage';
import { renderPage, stubFetchJson } from '../test/render';

const questions = [
  { id: '1', slug: 'two-sum', title: 'Two Sum', difficulty: 'Easy', tags: ['array', 'hash-table'] },
  { id: '2', slug: 'valid-parentheses', title: 'Valid Parentheses', difficulty: 'Easy', tags: ['stack'] },
  { id: '3', slug: 'maximum-subarray', title: 'Maximum Subarray Sum', difficulty: 'Medium', tags: ['array'] },
];

describe('QuestionsPage', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('lists every question with its difficulty', async () => {
    stubFetchJson({ body: questions });

    renderPage(<QuestionsPage />);

    expect(await screen.findByRole('link', { name: /Two Sum/ })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Maximum Subarray Sum/ })).toBeInTheDocument();
    // Scoped to the list: "Easy" is also an option in the difficulty filter.
    expect(within(screen.getByRole('list')).getAllByText('Easy')).toHaveLength(2);
  });

  it('links each question to its slug, which is the public URL', async () => {
    stubFetchJson({ body: questions });

    renderPage(<QuestionsPage />);

    expect(await screen.findByRole('link', { name: /Two Sum/ })).toHaveAttribute('href', '/problems/two-sum');
  });

  it('filters by difficulty', async () => {
    stubFetchJson({ body: questions });
    renderPage(<QuestionsPage />);
    await screen.findByRole('link', { name: /Two Sum/ });

    await userEvent.selectOptions(screen.getByLabelText('Difficulty'), 'Medium');

    expect(screen.getByRole('link', { name: /Maximum Subarray Sum/ })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Two Sum/ })).not.toBeInTheDocument();
  });

  it('filters by topic, offering only topics that exist', async () => {
    stubFetchJson({ body: questions });
    renderPage(<QuestionsPage />);
    await screen.findByRole('link', { name: /Two Sum/ });

    await userEvent.selectOptions(screen.getByLabelText('Topic'), 'stack');

    expect(screen.getByRole('link', { name: /Valid Parentheses/ })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Two Sum/ })).not.toBeInTheDocument();
  });

  it('says so when a filter combination matches nothing', async () => {
    stubFetchJson({ body: questions });
    renderPage(<QuestionsPage />);
    await screen.findByRole('link', { name: /Two Sum/ });

    await userEvent.selectOptions(screen.getByLabelText('Difficulty'), 'Hard');

    expect(screen.getByText('No questions match that filter.')).toBeInTheDocument();
  });

  it('reports a failure instead of showing an empty list', async () => {
    stubFetchJson({ status: 500, body: { title: 'api.error.unknown', detail: 'An unexpected error occurred.' } });

    renderPage(<QuestionsPage />);

    // An empty page would read as "no questions exist", which is a different and wrong message.
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('An unexpected error occurred.'));
  });
});
