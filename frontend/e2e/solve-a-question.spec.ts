import { expect, test, type Page } from '@playwright/test';

/**
 * The whole product in one flow: browse, open a question, write code, submit, and get a verdict
 * back from a Judge that really ran it in a container.
 *
 * Everything here is real -- no stubs, no fakes -- so a failure means a student would have hit it.
 */

const twoSumInPython = `import sys
d = sys.stdin.read().split()
n, t = int(d[0]), int(d[1])
a = list(map(int, d[2:2+n]))
seen = {}
for i, x in enumerate(a):
    if t - x in seen:
        print(seen[t - x], i)
        break
    seen.setdefault(x, i)
`;

/** The Api itself, for the one assertion that has to bypass the browser entirely. */
const apiBaseUrl = process.env.E2E_API_BASE_URL ?? 'http://localhost:8080';

/** Types into Monaco, which owns its own DOM and ignores a plain fill(). */
async function writeSolution(page: Page, source: string) {
  const editor = page.locator('.monaco-editor').first();
  await expect(editor).toBeVisible();

  await editor.click();
  await page.keyboard.press('ControlOrMeta+A');
  await page.keyboard.press('Delete');
  // Monaco auto-indents and auto-closes brackets, so paste through the clipboard API instead of
  // typing character by character, which would produce mangled Python.
  await page.evaluate(async (text) => navigator.clipboard.writeText(text), source);
  await page.keyboard.press('ControlOrMeta+V');
}

test('a student can browse to a question and see it', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByRole('heading', { name: 'Questions' })).toBeVisible();
  await page.getByRole('link', { name: /Two Sum/ }).click();

  await expect(page).toHaveURL(/\/problems\/two-sum$/);
  await expect(page.getByRole('heading', { name: 'Two Sum', level: 1 })).toBeVisible();
  // The statement is authored markdown, rendered as real headings.
  await expect(page.getByRole('heading', { name: 'Input', level: 2 })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Example 1' })).toBeVisible();
});

test("the editor opens with the question's starter, not an empty page", async ({ page }) => {
  await page.goto('/problems/two-sum');

  // Authored in content/questions/two-sum/starters/python.py: the stdin parsing is already
  // written, so a solver fills in one method instead of rediscovering the input format.
  const editor = page.locator('.monaco-editor .view-lines');
  await expect(editor).toContainText('two_sum');
  await expect(editor).toContainText('Your code here');

  // Switching language swaps the skeleton, because nothing has been typed yet.
  await page.getByLabel('Language').selectOption('csharp');
  await expect(editor).toContainText('TwoSum');
});

test('a correct solution is accepted, with every test case reported', async ({ page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await page.goto('/problems/two-sum');

  await writeSolution(page, twoSumInPython);
  await page.getByRole('button', { name: 'Submit' }).click();

  // It is judged asynchronously: this waits for the verdict the Judge actually produced.
  await expect(page.getByRole('status').filter({ hasText: 'Accepted' })).toBeVisible({ timeout: 60_000 });
  await expect(page.getByText('Test 1')).toBeVisible();
  // Hidden cases are reported as pass/fail only.
  await expect(page.getByText('hidden').first()).toBeVisible();
});

test('a wrong solution is rejected and shows what the code printed', async ({ page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await page.goto('/problems/two-sum');

  await writeSolution(page, "print('0 0')\n");
  await page.getByRole('button', { name: 'Submit' }).click();

  await expect(page.getByRole('status').filter({ hasText: 'Wrong answer' })).toBeVisible({ timeout: 60_000 });
  // The sample case that disagreed shows the student their own output.
  await expect(page.getByText('Your output')).toBeVisible();
});

test('code that crashes is reported as a runtime error, not as a wrong answer', async ({ page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await page.goto('/problems/two-sum');

  await writeSolution(page, "raise ValueError('boom')\n");
  await page.getByRole('button', { name: 'Submit' }).click();

  await expect(page.getByRole('status').filter({ hasText: 'Runtime error' })).toBeVisible({ timeout: 60_000 });
});

test('filtering the list by difficulty narrows it', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('link', { name: /Two Sum/ })).toBeVisible();

  await page.getByLabel('Difficulty').selectOption('Medium');

  await expect(page.getByRole('link', { name: /Maximum Subarray Sum/ })).toBeVisible();
  await expect(page.getByRole('link', { name: /Two Sum/ })).toBeHidden();
});

test('the Api refuses a submission from a caller it cannot identify', async ({ request }) => {
  // Every other test here submits successfully, which only proves the browser is sending a token.
  // This proves the Api would have rejected it otherwise -- straight to the Api, no token, no
  // browser. Without it, an Api that had stopped enforcing anything would pass this whole suite.
  const questions = await request.get(`${apiBaseUrl}/api/v1/questions`);
  expect(questions.ok()).toBeTruthy(); // reading the bank stays public: it is what search engines index

  const response = await request.post(`${apiBaseUrl}/api/v1/submissions`, {
    data: { questionId: (await questions.json())[0].id, language: 'python', sourceCode: 'print(1)' },
  });

  expect(response.status()).toBe(401);
  // And it says so the same way every other failure does, rather than returning nothing.
  expect((await response.json()).title).toBe('api.error.unauthorized');
});
