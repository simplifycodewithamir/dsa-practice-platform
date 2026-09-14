import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { useQuestions } from '../api/queries';
import type { Difficulty } from '../api/client';
import DifficultyBadge from '../components/DifficultyBadge';

const difficulties: Difficulty[] = ['Easy', 'Medium', 'Hard'];

export default function QuestionsPage() {
  const { data: questions, isPending, isError, error } = useQuestions();
  const [difficulty, setDifficulty] = useState<Difficulty | 'All'>('All');
  const [tag, setTag] = useState('All');

  // Filtering happens in the browser: the whole list is a few dozen rows, and a round trip per
  // keystroke would be slower and more code on both sides. Revisit if the bank grows into hundreds.
  const tags = useMemo(
    () => [...new Set((questions ?? []).flatMap((question) => question.tags ?? []))].sort(),
    [questions],
  );

  const visible = useMemo(
    () =>
      (questions ?? []).filter(
        (question) =>
          (difficulty === 'All' || question.difficulty === difficulty) &&
          (tag === 'All' || (question.tags ?? []).includes(tag)),
      ),
    [questions, difficulty, tag],
  );

  if (isPending) {
    return <p className="text-slate-600">Loading questions…</p>;
  }

  if (isError) {
    return (
      <p role="alert" className="rounded-md bg-rose-50 p-4 text-rose-800">
        Could not load the questions. {error.message}
      </p>
    );
  }

  return (
    <div>
      <h1 className="text-2xl font-semibold">Questions</h1>
      <p className="mt-1 text-slate-600">Solve them in the browser; your code runs in a sandbox.</p>

      <div className="mt-6 flex flex-wrap gap-4">
        <div>
          <label htmlFor="difficulty" className="block text-sm font-medium text-slate-700">
            Difficulty
          </label>
          <select
            id="difficulty"
            value={difficulty}
            onChange={(event) => setDifficulty(event.target.value as Difficulty | 'All')}
            className="mt-1 rounded-md border border-slate-300 bg-white px-3 py-1.5 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-700"
          >
            <option value="All">All</option>
            {difficulties.map((value) => (
              <option key={value} value={value}>
                {value}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label htmlFor="tag" className="block text-sm font-medium text-slate-700">
            Topic
          </label>
          <select
            id="tag"
            value={tag}
            onChange={(event) => setTag(event.target.value)}
            className="mt-1 rounded-md border border-slate-300 bg-white px-3 py-1.5 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-700"
          >
            <option value="All">All</option>
            {tags.map((value) => (
              <option key={value} value={value}>
                {value}
              </option>
            ))}
          </select>
        </div>
      </div>

      {visible.length === 0 ? (
        <p className="mt-8 text-slate-600">No questions match that filter.</p>
      ) : (
        <ul className="mt-6 divide-y divide-slate-200 overflow-hidden rounded-lg border border-slate-200 bg-white">
          {visible.map((question) => (
            <li key={question.id}>
              <Link
                to={`/problems/${question.slug}`}
                className="flex flex-wrap items-center justify-between gap-2 px-4 py-3 hover:bg-slate-50 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-sky-700"
              >
                <span className="font-medium">{question.title}</span>
                <span className="flex items-center gap-2">
                  {(question.tags ?? []).map((value) => (
                    <span key={value} className="text-xs text-slate-500">
                      {value}
                    </span>
                  ))}
                  <DifficultyBadge difficulty={question.difficulty!} />
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
