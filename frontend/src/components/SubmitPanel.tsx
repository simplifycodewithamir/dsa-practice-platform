import { useState } from 'react';
import Editor from '@monaco-editor/react';
import { useCreateSubmission, useSubmission } from '../api/queries';
import { getSubmitterId } from '../lib/submitter';
import VerdictPanel from './VerdictPanel';

/** Matches Submissions:SupportedLanguages on the Api; both are v1 scope. */
const languages = [
  { id: 'python', label: 'Python', monaco: 'python', starter: '# Read from stdin, print the answer.\n' },
  { id: 'csharp', label: 'C#', monaco: 'csharp', starter: '// Read from stdin, print the answer.\n' },
] as const;

export default function SubmitPanel({ questionId }: { questionId: string }) {
  const [language, setLanguage] = useState<(typeof languages)[number]>(languages[0]);
  // Annotated: the starters are literal types, and inference would pin the state to one of them.
  const [sourceCode, setSourceCode] = useState<string>(languages[0].starter);

  const createSubmission = useCreateSubmission();
  const submissionId = createSubmission.data?.id;
  const { data: submission } = useSubmission(submissionId);

  // Disabled while a submission is in flight or still being judged: a second submission would
  // replace the first one's result panel before anyone has read it.
  const judging = submission !== undefined && submission.status !== 'Completed';
  const busy = createSubmission.isPending || judging;

  function handleLanguageChange(id: string) {
    const next = languages.find((candidate) => candidate.id === id) ?? languages[0];
    setLanguage(next);

    // Only replaces code the user hasn't touched, so switching language never eats their work.
    setSourceCode((current: string) =>
      languages.some((candidate) => candidate.starter === current) ? next.starter : current,
    );
  }

  return (
    <section className="mt-8">
      <h2 className="text-lg font-semibold">Your solution</h2>

      <div className="mt-3 flex items-end gap-3">
        <div>
          <label htmlFor="language" className="block text-sm font-medium text-slate-700">
            Language
          </label>
          <select
            id="language"
            value={language.id}
            onChange={(event) => handleLanguageChange(event.target.value)}
            className="mt-1 rounded-md border border-slate-300 bg-white px-3 py-1.5 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-700"
          >
            {languages.map((candidate) => (
              <option key={candidate.id} value={candidate.id}>
                {candidate.label}
              </option>
            ))}
          </select>
        </div>

        <button
          type="button"
          disabled={busy || sourceCode.trim().length === 0}
          onClick={() =>
            createSubmission.mutate({
              questionId,
              userId: getSubmitterId(),
              language: language.id,
              sourceCode,
            })
          }
          className="rounded-md bg-slate-900 px-4 py-2 font-medium text-white disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-700"
        >
          {busy ? 'Judging…' : 'Submit'}
        </button>
      </div>

      <div className="mt-3 overflow-hidden rounded-lg border border-slate-200">
        <Editor
          height="360px"
          language={language.monaco}
          value={sourceCode}
          onChange={(value) => setSourceCode(value ?? '')}
          options={{
            minimap: { enabled: false },
            fontSize: 14,
            scrollBeyondLastLine: false,
            automaticLayout: true,
          }}
          // Shown while Monaco's bundle loads, and by jsdom in tests, which has no canvas.
          loading={<p className="p-4 text-slate-500">Loading editor…</p>}
        />
      </div>

      {createSubmission.isError && (
        <p role="alert" className="mt-3 rounded-md bg-rose-50 p-3 text-rose-800">
          Could not submit. {createSubmission.error.message}
        </p>
      )}

      {submission && (
        <div className="mt-4">
          <VerdictPanel submission={submission} />
        </div>
      )}
    </section>
  );
}
