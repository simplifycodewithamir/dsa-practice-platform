import { useState } from 'react';
import Editor from '@monaco-editor/react';
import { useCreateSubmission, useSubmission } from '../api/queries';
import VerdictPanel from './VerdictPanel';

/**
 * Matches Submissions:SupportedLanguages on the Api; both are v1 scope. `fallback` is only used for
 * a question that has no starter authored for that language yet -- the real skeletons are content
 * (content/questions/<slug>/starters/) and arrive on the question.
 */
const languages = [
  { id: 'python', label: 'Python', monaco: 'python', fallback: '# Read from stdin, print the answer.\n' },
  { id: 'csharp', label: 'C#', monaco: 'csharp', fallback: '// Read from stdin, print the answer.\n' },
] as const;

type Language = (typeof languages)[number];

export default function SubmitPanel({
  questionId,
  starters = {},
}: {
  questionId: string;
  /** Language id to skeleton, as authored for this question. */
  starters?: Record<string, string>;
}) {
  const starterFor = (candidate: Language) => starters[candidate.id] ?? candidate.fallback;

  const [language, setLanguage] = useState<Language>(languages[0]);
  // Lazy: starterFor reads a prop, and an eager call would re-run this on every render for nothing.
  const [sourceCode, setSourceCode] = useState<string>(() => starterFor(languages[0]));

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
    // "Untouched" means any language's starter for this question, not just the current one's.
    setSourceCode((current: string) =>
      languages.some((candidate) => starterFor(candidate) === current) ? starterFor(next) : current,
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
            // No user id: who is submitting is the Api's decision, from the caller's identity
            // (item 19). Item 20 adds signing in and a token on this request.
            createSubmission.mutate({ questionId, language: language.id, sourceCode })
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
