import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError, api, setAccessTokenProvider } from './client';

describe('api client', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    setAccessTokenProvider(undefined);
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

  it('sends the access token when the app is signed in', async () => {
    const fetchMock = stubFetch({ json: async () => ({}) });
    setAccessTokenProvider(() => 'a.b.c');

    await api.createSubmission({ questionId: 'q1', language: 'python', sourceCode: 'print(1)' });

    expect(fetchMock.mock.calls[0][1].headers.authorization).toBe('Bearer a.b.c');
  });

  it('reads the token per request, so a renewed one is used without re-wiring anything', async () => {
    const fetchMock = stubFetch({ json: async () => [] });
    let token = 'first';
    setAccessTokenProvider(() => token);

    await api.listQuestions();
    token = 'renewed';
    await api.listQuestions();

    expect(fetchMock.mock.calls[0][1].headers.authorization).toBe('Bearer first');
    expect(fetchMock.mock.calls[1][1].headers.authorization).toBe('Bearer renewed');
  });

  it('sends no authorization header when nobody is signed in', async () => {
    // The question bank is public; an empty "Bearer " header would be a malformed request, and a
    // stale one would be worse.
    const fetchMock = stubFetch({ json: async () => [] });
    setAccessTokenProvider(() => undefined);

    await api.listQuestions();

    expect(fetchMock.mock.calls[0][1].headers.authorization).toBeUndefined();
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
