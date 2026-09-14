import type { Submission, Verdict } from '../api/client';

type KnownVerdict = NonNullable<Verdict>;

const verdictStyles: Partial<Record<KnownVerdict, string>> = {
  Accepted: 'bg-emerald-50 text-emerald-800 ring-emerald-600/20',
  WrongAnswer: 'bg-rose-50 text-rose-800 ring-rose-600/20',
  TimeLimitExceeded: 'bg-amber-50 text-amber-900 ring-amber-600/20',
  MemoryLimitExceeded: 'bg-amber-50 text-amber-900 ring-amber-600/20',
  RuntimeError: 'bg-rose-50 text-rose-800 ring-rose-600/20',
  CompilationError: 'bg-rose-50 text-rose-800 ring-rose-600/20',
  InternalError: 'bg-slate-100 text-slate-800 ring-slate-500/20',
};

/** Spelled out, because "TimeLimitExceeded" is a wire value, not something to show a student. */
const verdictLabels: Partial<Record<KnownVerdict, string>> = {
  Accepted: 'Accepted',
  WrongAnswer: 'Wrong answer',
  TimeLimitExceeded: 'Time limit exceeded',
  MemoryLimitExceeded: 'Memory limit exceeded',
  RuntimeError: 'Runtime error',
  CompilationError: 'Compilation error',
  InternalError: 'Judge error — not your fault, try again',
};

export default function VerdictPanel({ submission }: { submission: Submission }) {
  if (submission.status !== 'Completed') {
    return (
      <div role="status" className="rounded-lg border border-slate-200 bg-white p-4 text-slate-700">
        <span className="font-medium">Judging…</span>
        <p className="mt-1 text-sm text-slate-500">Your code is running against the test cases.</p>
      </div>
    );
  }

  const verdict = submission.verdict as KnownVerdict;
  const results = submission.testResults ?? [];

  return (
    <div className="rounded-lg border border-slate-200 bg-white p-4">
      <p
        role="status"
        className={`inline-flex rounded-md px-2 py-1 text-sm font-semibold ring-1 ring-inset ${verdictStyles[verdict]}`}
      >
        {verdictLabels[verdict] ?? verdict}
      </p>

      {submission.compileOutput && (
        <pre className="mt-3 overflow-x-auto rounded bg-slate-900 p-3 text-xs text-slate-100">
          {submission.compileOutput}
        </pre>
      )}

      {results.length > 0 && (
        <ul className="mt-4 space-y-2">
          {results.map((result) => (
            <li key={result.ordinal} className="rounded-md border border-slate-200 p-3 text-sm">
              <div className="flex items-center justify-between">
                <span className="font-medium">
                  Test {result.ordinal}
                  {result.isHidden && <span className="ml-2 text-xs font-normal text-slate-500">hidden</span>}
                </span>
                <span className={result.passed ? 'text-emerald-700' : 'text-rose-700'}>
                  {result.passed ? 'passed' : 'failed'}
                  <span className="ml-2 text-slate-500">{result.executionTimeMs} ms</span>
                </span>
              </div>

              {/* Hidden cases report pass/fail and nothing else: the Api never sends their output,
                  so they cannot be reconstructed one submission at a time. */}
              {!result.passed && !result.isHidden && (result.actualOutput || result.errorMessage) && (
                <dl className="mt-2 space-y-1">
                  {result.actualOutput && (
                    <>
                      <dt className="text-xs text-slate-500">Your output</dt>
                      <dd>
                        <pre className="overflow-x-auto rounded bg-slate-100 p-2 text-xs">{result.actualOutput}</pre>
                      </dd>
                    </>
                  )}
                  {result.errorMessage && (
                    <>
                      <dt className="text-xs text-slate-500">Error</dt>
                      <dd>
                        <pre className="overflow-x-auto rounded bg-slate-900 p-2 text-xs text-slate-100">
                          {result.errorMessage}
                        </pre>
                      </dd>
                    </>
                  )}
                </dl>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
