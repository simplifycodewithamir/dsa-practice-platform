import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import SubmitPanel from './SubmitPanel';
import { renderPage, stubFetchJson } from '../test/render';

// Monaco needs a real canvas, which jsdom has not got. The editor is a controlled textarea here so
// the tests can exercise what this component is actually responsible for: language choice,
// submitting, polling and showing the verdict.
vi.mock('@monaco-editor/react', () => ({
  default: ({ value, onChange }: { value: string; onChange: (value: string) => void }) => (
    <textarea aria-label="Source code" value={value} onChange={(event) => onChange(event.target.value)} />
  ),
}));

const pending = { id: 's1', questionId: 'q1', status: 'Pending', verdict: null, testResults: [] };
const accepted = {
  ...pending,
  status: 'Completed',
  verdict: 'Accepted',
  testResults: [{ ordinal: 1, isHidden: false, passed: true, executionTimeMs: 11, actualOutput: '5', errorMessage: null }],
};

describe('SubmitPanel', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('submits the chosen language and the edited code', async () => {
    const fetchMock = stubFetchJson({ body: pending }, { body: accepted });
    renderPage(<SubmitPanel questionId="q1" />);

    await userEvent.selectOptions(screen.getByLabelText('Language'), 'csharp');
    await userEvent.clear(screen.getByLabelText('Source code'));
    await userEvent.type(screen.getByLabelText('Source code'), 'Console.WriteLine(1);');
    await userEvent.click(screen.getByRole('button', { name: 'Submit' }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    const body = JSON.parse(fetchMock.mock.calls[0][1].body);
    expect(body).toEqual({ questionId: 'q1', language: 'csharp', sourceCode: 'Console.WriteLine(1);' });
    // No userId: sending one would be a claim the caller is not entitled to make.
    expect(body.userId).toBeUndefined();
  });

  it('polls until the submission is judged, then shows the verdict', async () => {
    stubFetchJson({ body: pending }, { body: pending }, { body: accepted });
    renderPage(<SubmitPanel questionId="q1" />);

    await userEvent.click(screen.getByRole('button', { name: 'Submit' }));

    // Starts as "judging", and settles on the verdict without anyone refreshing.
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('Judging…'));
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('Accepted'), { timeout: 5000 });
  });

  it('keeps the submit button disabled while judging', async () => {
    stubFetchJson({ body: pending });
    renderPage(<SubmitPanel questionId="q1" />);

    await userEvent.click(screen.getByRole('button', { name: 'Submit' }));

    // A second submission would replace the first one's result before it had been read.
    await waitFor(() => expect(screen.getByRole('button', { name: 'Judging…' })).toBeDisabled());
  });

  it('cannot submit an empty editor', async () => {
    renderPage(<SubmitPanel questionId="q1" />);

    await userEvent.clear(screen.getByLabelText('Source code'));

    expect(screen.getByRole('button', { name: 'Submit' })).toBeDisabled();
  });

  it('keeps code the user wrote when the language changes', async () => {
    renderPage(<SubmitPanel questionId="q1" />);
    await userEvent.clear(screen.getByLabelText('Source code'));
    await userEvent.type(screen.getByLabelText('Source code'), 'my own work');

    await userEvent.selectOptions(screen.getByLabelText('Language'), 'csharp');

    expect(screen.getByLabelText('Source code')).toHaveValue('my own work');
  });

  it('reports a failed submission instead of looking like nothing happened', async () => {
    stubFetchJson({ status: 500, body: { title: 'api.error.unknown', detail: 'An unexpected error occurred.' } });
    renderPage(<SubmitPanel questionId="q1" />);

    await userEvent.click(screen.getByRole('button', { name: 'Submit' }));

    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('An unexpected error occurred.'));
  });
});
