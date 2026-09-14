import type { components } from './schema';

/**
 * Every type here comes from the Api's OpenAPI document (`npm run generate:api`), so a contract
 * change breaks the build rather than the page. Nothing in this file redeclares a server shape.
 */
export type QuestionSummary = components['schemas']['QuestionSummaryResponse'];
export type QuestionDetail = components['schemas']['QuestionDetailResponse'];
export type Submission = components['schemas']['SubmissionResponse'];
export type SubmissionTestResult = components['schemas']['SubmissionTestResultResponse'];
export type CreateSubmission = components['schemas']['CreateSubmissionRequest'];
export type Difficulty = components['schemas']['QuestionDifficulty'];
export type Verdict = components['schemas']['SubmissionVerdict'];
export type ProblemDetails = components['schemas']['ProblemDetails'];

/** Same origin in dev (Vite proxies /api); an absolute URL in production. */
const baseUrl = import.meta.env.VITE_API_BASE_URL ?? '';

/**
 * A failed request carries the Api's ProblemDetails, so the UI can say what went wrong instead of
 * "something failed".
 */
export class ApiError extends Error {
  // Declared as fields rather than constructor parameter properties: the tsconfig enables
  // erasableSyntaxOnly, so only syntax that erases cleanly to JavaScript is allowed.
  readonly status: number;
  readonly problem?: ProblemDetails;

  constructor(status: number, problem?: ProblemDetails) {
    super(problem?.detail ?? problem?.title ?? `Request failed with status ${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, {
    ...init,
    headers: {
      accept: 'application/json',
      ...(init?.body ? { 'content-type': 'application/json' } : {}),
      ...init?.headers,
    },
  });

  if (!response.ok) {
    // Error bodies are ProblemDetails by convention, but a proxy or a crash can return anything.
    const problem = await response
      .json()
      .then((body) => body as ProblemDetails)
      .catch(() => undefined);

    throw new ApiError(response.status, problem);
  }

  return (await response.json()) as T;
}

export const api = {
  listQuestions: () => request<QuestionSummary[]>('/api/v1/questions'),

  getQuestion: (slug: string) => request<QuestionDetail>(`/api/v1/questions/${encodeURIComponent(slug)}`),

  createSubmission: (submission: CreateSubmission) =>
    request<Submission>('/api/v1/submissions', {
      method: 'POST',
      body: JSON.stringify(submission),
    }),

  getSubmission: (id: string) => request<Submission>(`/api/v1/submissions/${encodeURIComponent(id)}`),
};
