import { Link, useParams } from 'react-router';
import { useQuestion } from '../api/queries';
import { ApiError } from '../api/client';
import DifficultyBadge from '../components/DifficultyBadge';
import Markdown from '../components/Markdown';

export default function QuestionPage() {
  const { slug = '' } = useParams();
  const { data: question, isPending, isError, error } = useQuestion(slug);

  if (isPending) {
    return <p className="text-slate-600">Loading…</p>;
  }

  if (isError) {
    const notFound = error instanceof ApiError && error.status === 404;

    return (
      <div role="alert">
        <h1 className="text-2xl font-semibold">{notFound ? 'No such question' : 'Could not load this question'}</h1>
        <p className="mt-2 text-slate-600">{notFound ? `Nothing is published at “${slug}”.` : error.message}</p>
        <Link to="/" className="mt-4 inline-block text-sky-700 underline">
          Back to the questions
        </Link>
      </div>
    );
  }

  return (
    <article>
      <div className="flex flex-wrap items-center gap-3">
        <h1 className="text-2xl font-semibold">{question.title}</h1>
        <DifficultyBadge difficulty={question.difficulty!} />
      </div>

      <div className="mt-2 flex flex-wrap items-center gap-3 text-sm text-slate-500">
        {(question.tags ?? []).map((tag) => (
          <span key={tag}>{tag}</span>
        ))}
        <span>·</span>
        {/* The limits are the question's own, and the Judge enforces exactly these. */}
        <span>{question.timeLimitMs} ms</span>
        <span>{question.memoryLimitMb} MB</span>
      </div>

      <div className="mt-6">
        <Markdown>{question.description ?? ''}</Markdown>
      </div>

      <section className="mt-8">
        <h2 className="text-lg font-semibold">Examples</h2>
        <div className="mt-3 grid gap-4 sm:grid-cols-2">
          {(question.sampleTestCases ?? []).map((testCase) => (
            <div key={testCase.ordinal} className="rounded-lg border border-slate-200 bg-white p-4">
              <h3 className="text-sm font-medium text-slate-500">Example {testCase.ordinal}</h3>
              <dl className="mt-2 space-y-2 text-sm">
                <div>
                  <dt className="text-slate-500">Input</dt>
                  <dd>
                    <pre className="mt-1 overflow-x-auto rounded bg-slate-900 p-2 text-slate-100">{testCase.input}</pre>
                  </dd>
                </div>
                <div>
                  <dt className="text-slate-500">Output</dt>
                  <dd>
                    <pre className="mt-1 overflow-x-auto rounded bg-slate-900 p-2 text-slate-100">
                      {testCase.expectedOutput}
                    </pre>
                  </dd>
                </div>
              </dl>
            </div>
          ))}
        </div>
      </section>
    </article>
  );
}
