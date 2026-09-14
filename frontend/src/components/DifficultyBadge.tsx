import type { Difficulty } from '../api/client';

const styles: Record<Difficulty, string> = {
  Easy: 'bg-emerald-50 text-emerald-700 ring-emerald-600/20',
  Medium: 'bg-amber-50 text-amber-800 ring-amber-600/20',
  Hard: 'bg-rose-50 text-rose-700 ring-rose-600/20',
};

export default function DifficultyBadge({ difficulty }: { difficulty: Difficulty }) {
  return (
    <span className={`inline-flex rounded-md px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${styles[difficulty]}`}>
      {difficulty}
    </span>
  );
}
