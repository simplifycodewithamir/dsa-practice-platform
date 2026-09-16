import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError, api } from './client';

describe('api client', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  function stubFetch(response: Partial<Response> & { json: () => Promise<unknown> }) {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 200, ...response });
    vi.stubGlobal('fetch', fetchMock);
    return fetchMock;
  }

  it('requests the questions collection', async () => {
    const fetchMock = stubFetch({ json: async () => [] });

    await api.listQuestions();

    expect(fetchMock).toHaveBeenCalledWith('/api/v1/questions', expect.objectContaining({}));
  });

  it('escapes the slug so a crafted value cannot alter the path', async () => {
    const fetchMock = stubFetch({ json: async () => ({}) });

    await api.getQuestion('two sum/../../etc');

    expect(fetchMock.mock.calls[0][0]).toBe('/api/v1/questions/two%20sum%2F..%2F..%2Fetc');
  });

  it('sends a submission as json', async () => {
    const fetchMock = stubFetch({ json: async () => ({}) });
    const submission = { questionId: 'q1', userId: 'u1', language: 'python', sourceCode: 'print(1)' };

    await api.createSubmission(submission);

    const [, init] = fetchMock.mock.calls[0];
    expect(init.method).toBe('POST');
    expect(init.headers['content-type']).toBe('application/json');
    expect(JSON.parse(init.body)).toEqual(submission);
  });

  it('throws an ApiError carrying the ProblemDetails the Api returned', async () => {
    stubFetch({
      ok: false,
      status: 404,
      json: async () => ({ title: 'api.error.notfound', status: 404, detail: "Question 'nope' was not found." }),
    });

    // The UI needs to say what went wrong, not just that something did.
    const error = await api.getQuestion('nope').then(() => undefined, (caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    if (!(error instanceof ApiError)) throw new Error('expected an ApiError');
    expect(error.status).toBe(404);
    expect(error.message).toBe("Question 'nope' was not found.");
    expect(error.problem?.title).toBe('api.error.notfound');
  });

  it('still fails usefully when the error body is not json', async () => {
    // A proxy or a crash can return html; the client must not throw a parse error over it.
    stubFetch({ ok: false, status: 502, json: async () => Promise.reject(new Error('not json')) });

    const error = await api.listQuestions().then(() => undefined, (caught: unknown) => caught);

    if (!(error instanceof ApiError)) throw new Error('expected an ApiError');
    expect(error.status).toBe(502);
    expect(error.message).toContain('502');
  });
});
