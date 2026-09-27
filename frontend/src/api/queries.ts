import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type CreateSubmission } from './client';

/** Matches the Api's own default page size, so the first page needs no query string to agree with it. */
export const historyPageSize = 20;

/** One place that knows how a cache key is shaped, so invalidation can't drift from fetching. */
export const queryKeys = {
  questions: ['questions'] as const,
  question: (slug: string) => ['questions', slug] as const,
  submission: (id: string) => ['submissions', id] as const,
  account: ['account'] as const,
  accountSubmissions: (page: number) => ['account', 'submissions', page] as const,
};

export function useQuestions() {
  return useQuery({
    queryKey: queryKeys.questions,
    queryFn: api.listQuestions,
  });
}

export function useQuestion(slug: string) {
  return useQuery({
    queryKey: queryKeys.question(slug),
    queryFn: () => api.getQuestion(slug),
  });
}

/**
 * A submission is judged asynchronously, so this polls until it reaches a terminal state and then
 * stops. Polling (rather than a socket) is decision D11: a judge run takes seconds.
 */
export function useSubmission(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.submission(id ?? ''),
    queryFn: () => api.getSubmission(id!),
    enabled: id !== undefined,
    refetchInterval: (query) => (query.state.data?.status === 'Completed' ? false : 1000),
  });
}

export function useCreateSubmission() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (submission: CreateSubmission) => api.createSubmission(submission),
    onSuccess: (submission) => {
      // Seed the cache so the result page has something to show before its first poll.
      queryClient.setQueryData(queryKeys.submission(submission.id!), submission);
    },
  });
}

export function useAccount() {
  return useQuery({
    queryKey: queryKeys.account,
    queryFn: api.getAccount,
  });
}

export function useMySubmissions(page: number) {
  return useQuery({
    queryKey: queryKeys.accountSubmissions(page),
    queryFn: () => api.listMySubmissions(page, historyPageSize),
    // Keeps the previous page on screen while the next one loads, so paging doesn't blank the list
    // and shift everything below it.
    placeholderData: keepPreviousData,
  });
}

export function useDeleteAccount() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: api.deleteAccount,
    onSuccess: () => {
      // Removed, not invalidated. Invalidating would refetch GET /api/v1/me, which provisions a user
      // on first sight -- so the act of confirming the deletion would immediately create the account
      // again, with the caller still holding a valid token.
      queryClient.removeQueries({ queryKey: queryKeys.account });
    },
  });
}
