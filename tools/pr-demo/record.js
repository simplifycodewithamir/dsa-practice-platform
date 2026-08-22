// PR demo recorder — drives a running API through Scalar + the browser and
// records a video + screenshots for attaching to a GitHub PR (see the
// "PR evidence" convention in the git-workflow skill).
//
// Requires Docker (no local Node/Playwright install needed):
//
//   1. Start Postgres + the Api locally per the repo README, on a known port.
//   2. Fill in the SCENARIO section at the bottom of this file for the
//      endpoints this PR actually touches.
//   3. Run:
//
//        OUT_DIR=/path/to/<repo>-pr<number> \
//        API_PORT=51942 \
//        docker run --rm --add-host=host.docker.internal:host-gateway \
//          -v "$(pwd)/tools/pr-demo:/scripts" \
//          -v "$OUT_DIR:/output" \
//          -e API_PORT -e Q1 -e Q2 \
//          -w /scripts mcr.microsoft.com/playwright:v1.49.0-noble \
//          bash -lc "node record.js"
//
// Known Scalar client gotcha (as of Scalar's bundled version here): the
// "Test Request" panel's path-parameter and JSON-body editors are CodeMirror
// widgets whose visible text can diverge from what actually gets sent —
// typing into them looks right on screen but the request goes out with the
// original empty placeholders. Confirmed via request interception; not a
// scripting bug on our side. Workaround used below:
//   - Use the Scalar "Test Request" flow ONLY for parameterless GETs
//     (nothing to fill in, so nothing to desync).
//   - For anything with a path parameter, navigate the browser directly to
//     the real URL instead of filling the client's Variables field.
//   - For anything with a request body (POST/PUT/PATCH), issue the call via
//     Playwright's `context.request` API (a real HTTP call, just not driven
//     through the flaky editor) and render the result on a plain page via
//     resultPageHtml() below so it still shows up in the recording.
// If a future Scalar version fixes this, the Test Request flow can be used
// end-to-end again — try it first and fall back to this if it still desyncs.

const { chromium } = require('playwright');
const fs = require('fs');

const API_PORT = process.env.API_PORT || '51942';
const BASE = `http://host.docker.internal:${API_PORT}`;
const OUT = '/output';

function log(...args) { console.log(new Date().toISOString(), ...args); }

async function shot(page, name) {
  await page.screenshot({ path: `${OUT}/${name}.png`, fullPage: false });
  log('screenshot', name);
}

const sendBtn = (page) => page.getByRole('button', { name: /request to/i });
const closeClient = (page) => page.getByRole('button', { name: 'Close Client' });

async function doSend(page) {
  await sendBtn(page).click();
  await page.waitForTimeout(1500);
}

// Navigates to a raw JSON GET endpoint and ticks Chrome's built-in
// "Pretty-print" checkbox so the screenshot is legible.
async function gotoPretty(page, url) {
  await page.goto(url, { waitUntil: 'networkidle' });
  await page.waitForTimeout(400);
  const cb = page.locator('input[type="checkbox"]').first();
  if (await cb.count()) await cb.check().catch(() => {});
  await page.waitForTimeout(400);
}

function syntaxHighlight(obj) {
  const json = typeof obj === 'string' ? obj : JSON.stringify(obj, null, 2);
  return json
    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
    .replace(/(&quot;|")([^"]+)\1(:)/g, '<span class="k">$1$2$1</span>$3')
    .replace(/: (&quot;|")([^"]*)\1/g, ': <span class="s">$1$2$1</span>');
}

// Renders a plain request/response page for calls made via the request API
// (POST/PUT/PATCH, or anything the Scalar client can't reliably drive).
function resultPageHtml(title, method, url, status, requestBody, responseBody) {
  const statusColor = status < 300 ? '#22c55e' : status < 500 ? '#f59e0b' : '#ef4444';
  return `<!doctype html><html><head><meta charset="utf-8"><style>
    body { background:#1e1e2e; color:#cdd6f4; font-family: 'Cascadia Code', 'Fira Code', monospace; padding: 40px; }
    h1 { font-size: 20px; color:#89b4fa; margin-bottom: 24px; }
    .row { display:flex; gap:12px; align-items:center; margin-bottom: 16px; }
    .method { background:#89b4fa; color:#1e1e2e; font-weight:bold; padding: 4px 10px; border-radius:4px; }
    .url { font-size:15px; }
    .status { font-weight:bold; color:${statusColor}; padding: 4px 10px; border: 1px solid ${statusColor}; border-radius:4px; }
    .panel { background:#181825; border-radius:8px; padding:16px; margin-bottom:20px; }
    .label { color:#a6adc8; font-size:12px; text-transform:uppercase; margin-bottom:8px; letter-spacing:1px; }
    pre { margin:0; white-space:pre-wrap; word-break:break-all; font-size:14px; line-height:1.6; }
    .k { color:#89dceb; } .s { color:#a6e3a1; }
  </style></head><body>
    <h1>${title}</h1>
    <div class="row"><span class="method">${method}</span><span class="url">${url}</span><span class="status">${status}</span></div>
    ${requestBody ? `<div class="panel"><div class="label">Request Body</div><pre>${syntaxHighlight(requestBody)}</pre></div>` : ''}
    <div class="panel"><div class="label">Response Body</div><pre>${syntaxHighlight(responseBody)}</pre></div>
  </body></html>`;
}

async function main() {
  const browser = await chromium.launch();
  const context = await browser.newContext({
    viewport: { width: 1280, height: 900 },
    recordVideo: { dir: `${OUT}/video`, size: { width: 1280, height: 900 } },
  });
  const page = await context.newPage();
  const api = context.request;

  await page.goto(`${BASE}/scalar/`, { waitUntil: 'networkidle', timeout: 30000 });
  await page.waitForTimeout(1500);
  await shot(page, '01-scalar-home');

  // ---------------------------------------------------------------------
  // SCENARIO — edit this section per PR for the endpoints it touches.
  // Everything above is reusable; everything below is the example from
  // dsa-practice-platform PR #6 (Questions + Submissions endpoints), left
  // in place as a working template.
  // ---------------------------------------------------------------------

  const Q1 = process.env.Q1; // seed a question and export its id before running

  await page.getByText('QuestionsEndpoints', { exact: true }).first().click();
  await page.waitForTimeout(300);
  await page.getByText('SubmissionsEndpoints', { exact: true }).first().click();
  await page.waitForTimeout(500);
  await shot(page, '02-groups-expanded');

  const testButtons = page.getByRole('button', { name: 'Test Request' });
  // index 0 = /health, 1 = GET /questions, 2 = GET /questions/{id}, 3 = POST /submissions, 4 = GET /submissions/{id}

  // Parameterless GETs — safe to drive through Scalar's live Test Request client.
  await testButtons.nth(0).scrollIntoViewIfNeeded();
  await testButtons.nth(0).click();
  await page.waitForTimeout(600);
  await doSend(page);
  await shot(page, '03-get-health-response');
  await closeClient(page).click();
  await page.waitForTimeout(400);

  await testButtons.nth(1).scrollIntoViewIfNeeded();
  await testButtons.nth(1).click();
  await page.waitForTimeout(600);
  await shot(page, '04-get-questions-test-mode');
  await doSend(page);
  await shot(page, '05-get-questions-response');
  await closeClient(page).click();
  await page.waitForTimeout(400);

  // Path-parameterized GETs — direct browser navigation, not the Test Request client.
  await gotoPretty(page, `${BASE}/api/v1/questions/${Q1}`);
  await shot(page, '06-get-question-by-id-browser');

  await gotoPretty(page, `${BASE}/api/v1/questions/00000000-0000-0000-0000-000000000000`);
  await shot(page, '07-get-question-unknown-404-browser');

  // POST — via the request API, rendered on a plain result page.
  const validPayload = {
    questionId: Q1,
    userId: 'demo-user',
    language: 'csharp',
    sourceCode: 'Console.WriteLine(1);',
  };
  const createRes = await api.post(`${BASE}/api/v1/submissions`, { data: validPayload });
  const createBody = await createRes.json();
  log('create submission status:', createRes.status(), createBody);
  await page.setContent(resultPageHtml(
    'POST /api/v1/submissions — create a submission',
    'POST', '/api/v1/submissions', createRes.status(), validPayload, createBody,
  ));
  await shot(page, '08-post-submission-201');

  const submissionId = createBody.id;

  const invalidPayload = { ...validPayload, language: 'rust', sourceCode: 'fn main() {}' };
  const badRes = await api.post(`${BASE}/api/v1/submissions`, { data: invalidPayload });
  const badBody = await badRes.json();
  log('unsupported language status:', badRes.status(), badBody);
  await page.setContent(resultPageHtml(
    'POST /api/v1/submissions — unsupported language',
    'POST', '/api/v1/submissions', badRes.status(), invalidPayload, badBody,
  ));
  await shot(page, '09-post-submission-400');

  await gotoPretty(page, `${BASE}/api/v1/submissions/${submissionId}`);
  await shot(page, '10-get-submission-by-id-browser');

  await gotoPretty(page, `${BASE}/api/v1/submissions/00000000-0000-0000-0000-000000000000`);
  await shot(page, '11-get-submission-unknown-404-browser');

  await page.goto(`${BASE}/scalar/`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(1000);
  await shot(page, '12-scalar-closing');

  // ---------------------------------------------------------------------

  await context.close();
  await browser.close();
}

main();
