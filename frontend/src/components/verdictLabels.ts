import type { Verdict } from '../api/client';

export type KnownVerdict = NonNullable<Verdict>;

/**
 * Spelled out, because "TimeLimitExceeded" is a wire value, not something to show a student.
 *
 * Shared, so the wording cannot drift between the panel you see after submitting and the history
 * table. Where a place genuinely needs different words it overrides one entry and the difference is
 * visible at the override rather than hidden in a second copy of the list.
 */
export const verdictLabels: Record<KnownVerdict, string> = {
  Accepted: 'Accepted',
  WrongAnswer: 'Wrong answer',
  TimeLimitExceeded: 'Time limit exceeded',
  MemoryLimitExceeded: 'Memory limit exceeded',
  RuntimeError: 'Runtime error',
  CompilationError: 'Compilation error',
  InternalError: 'Judge error',
};
