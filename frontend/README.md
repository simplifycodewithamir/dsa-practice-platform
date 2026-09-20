# Frontend

React + TypeScript + Vite, per decision D10 in the root README.

## No Node on your machine? You don't need one

Everything below runs in a container, which is also how CI builds it:

```bash
docker run --rm -u "$(id -u):$(id -g)" -e HOME=/tmp -e npm_config_cache=/tmp/.npm \
  -v "$PWD:/repo" -w /repo/frontend --network host node:22-alpine sh -lc "npm ci && npm test"
```

With Node installed locally, the plain commands work as usual:

| Command | Does |
|---|---|
| `npm run dev` | Vite dev server on :5173, proxying `/api` to the Api on :8080 |
| `npm test` | Vitest (unit + component) |
| `npm run build` | type-check and production build |
| `npm run lint` | ESLint |
| `npm run generate:api` | regenerate `src/api/schema.d.ts` from the **running** Api's OpenAPI document |
| `npm run test:e2e` | Playwright, against a running full stack |

Playwright also needs browser **system** libraries, which `npx playwright install` does not install
and which need root. Without them, run the suite in Playwright's own image instead — same result,
nothing installed on the machine:

```bash
docker run --rm -u "$(id -u):$(id -g)" -e HOME=/tmp -e npm_config_cache=/tmp/.npm -e CI=1 \
  -v "$PWD:/repo" -w /repo/frontend --network host mcr.microsoft.com/playwright:v1.49.0-noble \
  sh -lc "npx playwright test"
```

## The API client is generated, not hand-written

`src/api/schema.d.ts` comes from the Api's OpenAPI document, and `src/api/client.ts` derives every
request and response type from it. A contract change therefore breaks the build rather than the
page. Regenerate it with the Api running (`docker compose --profile full-stack up -d`), and commit
the result so a build never needs a live Api.

## The editor opens with the question's starter, not an empty page

`SubmitPanel` gets a `starters` map (language id → skeleton) on the question and opens with the one
for the selected language. Those skeletons are **content**, authored under
`content/questions/<slug>/starters/` — see "Authoring a question" in the root README. A question
with no starter for the chosen language falls back to the generic comment defined in
`SubmitPanel.tsx`.

Switching language replaces the code only while it is still exactly some language's starter, so it
never eats work someone has typed.

## Signing in

`src/auth/` owns all of it — Authorization Code + PKCE via `oidc-client-ts`, held by
`react-oidc-context`. Nothing outside that folder knows which identity provider is in use, or
whether there is one: components read a small `Session` from `useSession()`, and the fetch layer is
handed a token through `setAccessTokenProvider`, because it is not a component and cannot use a
hook.

Configure it with `VITE_OIDC_*` (copy `.env.example` to `.env.local`). **With no authority
configured the app runs signed-out**: questions are readable and submitting is not offered, which
is what a checkout with no tenant of its own does. `VITE_DEV_ACCESS_TOKEN` then supplies a token
for the end-to-end stack, where the Api trusts a development signing key — `node
tools/mint-dev-token.mjs` produces both halves. A build with a real authority ignores it outright.

Access tokens are held **in memory only**, so a hard refresh re-runs the redirect (silently, when
the session at the provider is still alive). Only the PKCE verifier goes to `sessionStorage`,
because it has to survive the round trip. See `../docs/auth.md`.

## Why there is no CORS problem in development

The dev server proxies `/api` to the Api, so the browser sees a single origin. In production they
are different hosts, which is why the Api configures CORS explicitly from
`Cors:AllowedOrigins` — empty by default, so nothing cross-origin is allowed unless it is named.
