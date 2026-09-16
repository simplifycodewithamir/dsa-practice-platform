import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import VerdictPanel from './VerdictPanel';
import type { Submission } from '../api/client';

function submission(overrides: Partial<Submission> = {}): Submission {
  return {
    id: 's1',
    questionId: 'q1',
    userId: 'anon-1',
    language: 'python',
    status: 'Completed',
    verdict: 'Accepted',
    submittedAtUtc: '2026-01-01T00:00:00Z',
    testResults: [],
    ...overrides,
  } as Submission;
}

describe('VerdictPanel', () => {
  it('says it is still judging while the submission is pending', () => {
    render(<VerdictPanel submission={submission({ status: 'Pending', verdict: null })} />);

    expect(screen.getByRole('status')).toHaveTextContent('Judging…');
  });

  it('spells the verdict out instead of showing the wire value', () => {
    render(<VerdictPanel submission={submission({ verdict: 'TimeLimitExceeded' })} />);

    expect(screen.getByRole('status')).toHaveTextContent('Time limit exceeded');
    expect(screen.queryByText('TimeLimitExceeded')).not.toBeInTheDocument();
  });

  it('says a judge error is not the submitter’s fault', () => {
    render(<VerdictPanel submission={submission({ verdict: 'InternalError' })} />);

    expect(screen.getByRole('status')).toHaveTextContent('not your fault');
  });

  it('shows each test case with its time', () => {
    render(
      <VerdictPanel
        submission={submission({
          verdict: 'WrongAnswer',
          testResults: [
            { ordinal: 1, isHidden: false, passed: true, executionTimeMs: 12, actualOutput: '5', errorMessage: null },
            { ordinal: 2, isHidden: false, passed: false, executionTimeMs: 15, actualOutput: '9', errorMessage: null },
          ],
        })}
      />,
    );

    expect(screen.getByText('Test 1')).toBeInTheDocument();
    expect(screen.getByText(/12 ms/)).toBeInTheDocument();
    expect(screen.getByText('9')).toBeInTheDocument();
  });

  it('never shows output for a hidden test case, only whether it passed', () => {
    render(
      <VerdictPanel
        submission={submission({
          verdict: 'WrongAnswer',
          testResults: [
            {
              ordinal: 3,
              isHidden: true,
              passed: false,
              executionTimeMs: 20,
              // The Api nulls these for hidden cases; this asserts the UI wouldn't leak them even
              // if they arrived, because that is how hidden tests get reconstructed.
              actualOutput: 'leaked-output',
              errorMessage: 'leaked-error',
            },
          ],
        })}
      />,
    );

    expect(screen.getByText('hidden')).toBeInTheDocument();
    expect(screen.getByText('failed')).toBeInTheDocument();
    expect(screen.queryByText('leaked-output')).not.toBeInTheDocument();
    expect(screen.queryByText('leaked-error')).not.toBeInTheDocument();
  });

  it('shows compiler output when the code never ran', () => {
    render(
      <VerdictPanel
        submission={submission({ verdict: 'CompilationError', compileOutput: 'error CS1002: ; expected' })}
      />,
    );

    expect(screen.getByText('error CS1002: ; expected')).toBeInTheDocument();
  });
});
