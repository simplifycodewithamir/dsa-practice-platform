import { screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import QuestionPage from './QuestionPage';
import { renderPage, stubFetchJson } from '../test/render';

const question = {
  id: '1',
  slug: 'two-sum',
  title: 'Two Sum',
  description: '## Input\n\nTwo integers `a` and `b`.\n\n- first bullet\n- second bullet',
  difficulty: 'Easy',
  tags: ['array'],
  timeLimitMs: 1000,
  memoryLimitMb: 256,
  sampleTestCases: [
    { id: 't1', ordinal: 1, input: '4 9\n2 7 11 15', expectedOutput: '0 1' },
    { id: 't2', ordinal: 2, input: '3 6\n3 2 4', expectedOutput: '1 2' },
  ],
};

function renderQuestion() {
  return renderPage(<QuestionPage />, { path: '/problems/two-sum', route: '/problems/:slug' });
}

describe('QuestionPage', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('shows the title, difficulty and the limits the Judge enforces', async () => {
    stubFetchJson({ body: question });

    renderQuestion();

    expect(await screen.findByRole('heading', { name: 'Two Sum' })).toBeInTheDocument();
    expect(screen.getByText('Easy')).toBeInTheDocument();
    expect(screen.getByText('1000 ms')).toBeInTheDocument();
    expect(screen.getByText('256 MB')).toBeInTheDocument();
  });

  it('renders the statement as markdown rather than raw text', async () => {
    stubFetchJson({ body: question });

    renderQuestion();

    // The heading and list come from markdown syntax, so these prove it was parsed.
    expect(await screen.findByRole('heading', { name: 'Input', level: 2 })).toBeInTheDocument();
    expect(screen.getAllByRole('listitem')).toHaveLength(2);
  });

  it('shows every sample test case with its input and expected output', async () => {
    stubFetchJson({ body: question });

    renderQuestion();

    expect(await screen.findByRole('heading', { name: 'Example 1' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Example 2' })).toBeInTheDocument();
    // Testing Library normalises whitespace, so match the <pre> on its own text content. The <dd>
    // wrapper has the same text, hence matching the tag too.
    const inputs = screen.getAllByText(
      (_, element) => element?.tagName === 'PRE' && element.textContent === '4 9\n2 7 11 15',
    );
    expect(inputs).toHaveLength(1);
  });

  it('explains a 404 in its own terms rather than as a generic failure', async () => {
    stubFetchJson({ status: 404, body: { title: 'api.error.notfound', detail: "Question 'two-sum' was not found." } });

    renderQuestion();

    await waitFor(() => expect(screen.getByRole('heading', { name: 'No such question' })).toBeInTheDocument());
    expect(screen.getByRole('link', { name: 'Back to the questions' })).toBeInTheDocument();
  });

  it('reports other failures as failures, not as a missing question', async () => {
    stubFetchJson({ status: 500, body: { title: 'api.error.unknown', detail: 'An unexpected error occurred.' } });

    renderQuestion();

    await waitFor(() =>
      expect(screen.getByRole('heading', { name: 'Could not load this question' })).toBeInTheDocument(),
    );
  });
});
