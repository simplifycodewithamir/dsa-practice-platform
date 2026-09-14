# DSA Practice Platform

Free DSA question-practice platform, deployed for students. MVP-scoped, sequenced project — see `.claude/skills/dsa-practice-platform/SKILL.md` for architecture and conventions before making changes, and `CLAUDE.md` for always-on repo context (stack, run/build commands).

## Structure
```
.
├── CLAUDE.md                             # always-on project context
├── .claude/skills/dsa-practice-platform/SKILL.md   # Judge/sandbox architecture, loads on-demand
├── Directory.Packages.props              # NuGet Central Package Management
├── source/
│   ├── DsaPractice.slnx
│   ├── DsaPractice.Api/                  # Minimal API — Questions, Submissions (metadata only)
│   ├── DsaPractice.DataAccess/           # EF Core DbContext + entities, Postgres
│   ├── DsaPractice.ContentSeeding/       # reads content/questions/** and upserts it by slug
│   ├── DataMigrations/DsaPractice.DataMigrations.Postgres/   # migrations + the `migrator` app/image
│   ├── DsaPractice.Judge/                # Worker service — sandboxed code execution
│   └── DsaPractice.Contracts/            # shared RabbitMQ message DTOs
├── tests/
│   ├── DsaPractice.Api.UnitTests/
│   ├── DsaPractice.Api.IntegrationTests/
│   ├── DsaPractice.ContentSeeding.UnitTests/
│   ├── DsaPractice.ContentSeeding.IntegrationTests/
│   └── DsaPractice.Judge.UnitTests/
├── content/questions/<slug>/             # authored questions — see "Authoring a question"
├── docs/                                 # learning notes (e.g. dsa-containers-design.md)
├── frontend/                             # React + TypeScript (not yet scaffolded)
├── .github/workflows/dotnet.yml
└── docker-compose.yml                    # Postgres + RabbitMQ + migrator (+ api/judge under `full-stack`)
```

## What's already scaffolded
- Solution + all `.csproj` files wired with correct project references
- `Program.cs` skeletons for both Api and Judge, with TODOs marking exactly what's next
- Entity skeletons: `Question`, `TestCase`, `Submission`
- Shared message contracts: `SubmissionJudgeRequested`, `SubmissionJudged`
- Day-1 CI (GitHub Actions: restore → build → test → CodeQL → Docker images)
- docker-compose for local Postgres + RabbitMQ + both services

## Already DONE
1. **DbContext registration + first migration** — `DsaPracticeDbContext` registered in Api `Program.cs`, first EF Core migration generated and applied.
2. **Global exception handler → ProblemDetails mapping** — `GlobalExceptionHandler` + a small `NotFoundException`/`ConflictException`/`BadRequestException` hierarchy shaped by HTTP semantics, each mapping to the right status code and a safe `ProblemDetails` body (never leaks a raw exception message).
3. **Questions/Submissions endpoint implementations** (PR #6 — see the PR description and commit history for the full story):
   - `GET /api/v1/questions`, `GET /api/v1/questions/{id}` (hidden test cases filtered out), `POST /api/v1/submissions`, `GET /api/v1/submissions/{id}`.
   - Extracted `QuestionsService`/`SubmissionsService` — endpoints are thin HTTP adapters, services own the domain rules and inject `DbContext` directly (Scoped).
   - Tightened DTOs, endpoints, and the exception handler to `internal` — only `Program` stays public.
   - Supported submission languages moved from a hardcoded array to `Submissions:SupportedLanguages` in `appsettings.json`, via the Options pattern (`IOptionsSnapshot`, validated with `ValidateOnStart()`).
   - Split test projects into `DsaPractice.Api.UnitTests`/`DsaPractice.Api.IntegrationTests`; CI runs them as separate steps.
   - Upgraded all test projects to xUnit v3 + Microsoft.Testing.Platform (native `dotnet test` mode).
   - Adopted NuGet Central Package Management (`Directory.Packages.props` at the repo root) — no `.csproj` specifies a version.
   - Replaced deprecated `FluentValidation.AspNetCore` with core `FluentValidation` 12.x; bumped other outdated packages; dropped unused `SSH.NET`.
   - Fixed `AddOpenApi()` never being registered (Scalar had nothing to serve) and a Minimal API parameter-inference failure caused by an internal validator type not being discoverable via assembly scanning.
   - Automated EF Core migrations in `docker compose up` via a one-shot `migrator` service — no manual `dotnet ef` step needed; `api`/`judge` moved behind an opt-in `full-stack` profile.
   - Added `.vscode/launch.json`/`tasks.json` for F5 debugging in VS Code, and fixed a pre-existing gap where Serilog had zero sinks configured (the app logged nothing to console at all).
   - Built a Playwright-based PR demo recorder, then moved it to `my-notes-and-skills/tools/pr-demo/` as a shared, repo-agnostic template.

4. ~~**API authorization** (PR #8)~~ — **closed, not merged; superseded by D3/D4 below.** Its sound parts (`RequireAuthorization()`, identity from the token instead of a spoofable request field, the owner-or-Admin `SubmissionOwnershipFilter`, 401/403 ProblemDetails) come back in item 19 on top of a real `Users` table. Its custom HS256 token service and dev-token endpoint don't — they'd be deleted the day a real IdP arrives, and `Submission.UserId` storing the raw IdP `sub` would tie every submission to one IdP forever.
5. **Question model for real content** — the schema item 6's content files will fill:
   - `Question` gains `Slug` (unique, lowercase kebab-case), `Difficulty` as an enum, `Tags` (native Postgres `text[]`), per-question limits (`TimeLimitMs` per test case, `MemoryLimitMb`); `Description` holds the markdown statement. `TestCase` gains `Ordinal` (unique per question) — visible test cases are the samples.
   - `Submission` splits lifecycle from outcome: `Status` (Pending → Running → Completed) and a nullable `Verdict` (Accepted, WrongAnswer, TimeLimitExceeded, MemoryLimitExceeded, RuntimeError, CompilationError, InternalError), set exactly when Completed.
   - Invariants enforced in Postgres, not only in C# — the seeder (item 6) and the Judge result consumer (item 10) bypass the Api's validators: slug format/uniqueness, positive limits, ordinal uniqueness, verdict-iff-completed check, and a real `Submissions → Questions` foreign key (previously missing).
   - Enums stored and serialized by name (`HasConversion<string>()`, `JsonStringEnumConverter`). `GET /api/v1/questions/{id}` replaced by `GET /api/v1/questions/{slug}` — a regex route constraint sharing the DB check's pattern.
   - **Local DB note:** this migration requires an empty `Questions` table (a new slug/limits column has no meaningful default). If yours holds old demo rows, the migrator fails with `column "Tags" of relation "Questions" contains null values` (rolled back, nothing half-applied). Either delete just those rows (`delete from "Submissions"; delete from "TestCases"; delete from "Questions";` in psql), or wipe everything local with `docker compose --profile full-stack down -v` (deletes the Postgres + RabbitMQ volumes).
6. **Content seeder + first 3 questions** (D9) — questions live in `content/questions/<slug>/` and are upserted by slug (see "Authoring a question" below). Two Sum, Valid Parentheses and Maximum Subarray Sum are seeded, each with samples, hidden tests and a reference solution.
   - `DsaPractice.ContentSeeding` — `ContentLoader` (reads and validates the tree, reporting every broken question in one message) and `QuestionSeeder` (the upsert).
   - **Idempotent:** re-running against unchanged content writes nothing. Edits update in place, keeping question and test-case ids, because submissions and their per-test results point at them. A question in the database but absent from content is left alone, never deleted.
   - Ordinals are assigned by the loader (samples first, then hidden), so authors never hand-number test cases across two folders and collide on the unique index.
   - **The `migrator` is now a small console app** rather than a `dotnet ef database update` invocation: it applies migrations, then seeds, in one transaction-safe shot. Its image dropped from the SDK to the runtime, and no design-time tooling ships to a deployment. `dotnet ef migrations add` on the host is unchanged.
7. **Publish `SubmissionJudgeRequested`** (D5, D7) — creating a submission now publishes a judge request to RabbitMQ.
   - **Topology:** durable direct exchange `dsa.submissions` → durable queue `submission.judge-requested`, bound on the routing key of the same name. Declared (idempotently) when the Api opens its connection, so a publish never races an undeclared queue; the Judge will declare the same topology in item 9.
   - **Self-contained message** (D7): code, language, per-question limits, and *every* test case including hidden ones — the Judge never reads the Api's database. Hiding test cases is a read-API rule, not a judging one.
   - **Publisher confirms** (`CreateChannelOptions(publisherConfirmationsEnabled: true)`) plus persistent delivery and `mandatory: true`: the publish call completes only once the broker has taken responsibility, and throws if the message is nacked or unroutable. Without confirms, a dropped message looks like success.
   - **Connection opened lazily**, not at startup — a broker outage shouldn't stop an Api that still serves questions fine. One connection per process, a channel per publish.
   - **The dual-write gap was deliberate and left visible:** the row was committed, *then* the message published, so a broker outage meant a 500 for a submission that was in fact saved. Closed by item 8.
   - **One observation for later:** a message carrying Two Sum's large hidden test is ~290 KB. Fine for RabbitMQ now; revisit if test data grows (D7 notes the cap).
8. **Transactional outbox** (D6) — item 7's dual-write gap is closed: creating a submission never talks to the broker.
   - `OutboxMessages` table; the judge request is staged on the same `DbContext` and committed by the **same `SaveChanges`** as the submission. Either both exist or neither does.
   - `OutboxRelay` (a `BackgroundService`) polls and publishes. It claims rows with `FOR UPDATE SKIP LOCKED`, so a second instance skips claimed work rather than blocking or double-publishing.
   - Failures back off exponentially (2s, doubling, capped at 1 min) with the attempt count and last error recorded on the row; processed rows are purged after 7 days.
   - **At-least-once, not exactly-once:** if the broker confirms but the relay crashes before committing, the message is published again. Consumers must be idempotent — item 9's job.
   - **Verified against a real outage:** with RabbitMQ stopped, `POST /submissions` returns **201** (it was 500 in item 7); the row waits in the outbox through three backing-off attempts, and the relay publishes it by itself seconds after the broker comes back.

## Roadmap — work top to bottom; a merged item moves up to "Already DONE"

### Goals & constraints
- **Free to run.** The only unavoidable cost is the `.com` domain (~$10–15/yr). Everything else sits on free tiers; where a free tier might not hold, a cheap fallback is named.
- **Portfolio first, revenue later.** Built step by step with Claude Code; every item should be something worth explaining in an interview. Revenue later via Google AdSense — which needs public, crawlable, original content, so that shapes the frontend (D10) and Phase 6.
- **Learn as we go.** One item = one small PR, each listing the concepts it teaches. Nothing is built ahead of its item.
- **Local first.** Phases 1–4 run entirely on `docker compose`. Nothing is deployed before Phase 5.

### Key decisions
Not final — revisit any row whose *why* stops holding.

| # | Decision | Why |
|---|---|---|
| D1 | Host everything on **one free ARM VM** (Oracle Cloud Always Free, Ampere A1) running `docker compose` | The Judge needs a Docker daemon to spawn sandboxes; free PaaS tiers (App Service, Render, Railway, …) don't give you one. Consequence: images must build for `linux/arm64`. Fallback: Hetzner CAX11 (~€4/mo). |
| D2 | **Cloudflare** in front: DNS, TLS, Tunnel, Pages (frontend), R2 (backups), Turnstile | All free. The Tunnel means the VM has no inbound HTTP ports open, and there is no reverse proxy or certificate renewal to run yourself. |
| D3 | **Managed OIDC identity provider**. The Api is only a resource server; the SPA uses Authorization Code + PKCE; a thin local `Users` table with an internal `Guid` id, mapped from the token's `(iss, sub)` | No password storage, reset emails, MFA or bot-signup handling to own and get wrong on a public domain. Free tiers cover far more than MVP traffic. The internal id means switching IdP never orphans anyone's history. The IdP itself is picked in item 20 after a fresh pricing check. |
| D4 | Local dev tokens via **`dotnet user-jwts`**; no token-minting code in the Api | Built into the SDK and config-only (`Authentication:Schemes:Bearer`), so the production binary contains no dev-token endpoint that could ever be exposed. |
| D5 | **`RabbitMQ.Client` directly**, no MassTransit | You learn the real primitives (exchanges, acks, prefetch, dead-lettering, publisher confirms). MassTransit v9+ is also commercially licensed. |
| D6 | **Transactional outbox** for Api → RabbitMQ | Saving a submission and publishing its message touch two systems. Without an outbox, a crash between the two loses the message (submission stuck `Pending` forever) or the retry duplicates it. |
| D7 | Judge messages carry everything needed to judge: code, language, test cases, limits | The Judge never touches the Api's database — a clean service boundary. Cap message size; only move test data to object storage if it ever outgrows that. |
| D8 | **stdin/stdout judging** (Codeforces-style), not LeetCode-style function signatures | Language-agnostic: a new language is a new runner image, not per-question driver code for every language. Revisit in v2 if the UX calls for it. |
| D9 | Questions as **content-as-code** — files under `content/questions/<slug>/`, idempotently upserted by the `migrator` container — not EF data migrations | Fixing a typo in a statement shouldn't need a schema migration. Content gets reviewed in PRs like code, and scales past 30 questions without pain. |
| D10 | Frontend: **Vite + React + TypeScript + React Router v7 + TanStack Query + Tailwind + Monaco**; public pages **prerendered at build time**; static hosting on Cloudflare Pages | Organic traffic and AdSense both depend on Google indexing real HTML. Build-time prerendering gets that without running a Node SSR server. |
| D11 | Verdicts reach the browser by **polling** first; SSE only if needed | Simplest thing that works — a judge run takes seconds either way. |

### Phase 1 — Core judging loop (local, backend only)
The heart of the product. Submissions keep a client-supplied `userId` until Phase 3 — acceptable only because nothing is deployed yet.

9. **Judge consumer with a fake executor** — manual ack, prefetch, idempotency check, retry + dead-letter queue; a `FakeSandboxExecutor` returns canned verdicts and publishes `SubmissionJudged`.
   *Learn:* competing consumers, ack/nack/requeue, poison messages, dead-letter exchanges.
9. **Judge consumer with a fake executor** — the Judge now consumes judge requests, "runs" them and publishes `SubmissionJudged`; the Api stores the result in item 10.
   - **Acks last**, after the result is published: if the Judge dies mid-run the message was never acked, so the broker redelivers it. Prefetch is 1 — each submission will own a container.
   - **Dead-letter queue** for anything rejected: an unparseable payload, or a run that failed. Both exchanges and all three queues are declared by `RabbitMqTopology`, shared by Api and Judge, because RabbitMQ refuses a redeclaration that disagrees with what exists.
   - **A Judge failure still produces a verdict** (`InternalError`) as well as a dead-lettered copy — a submission stuck `Running` forever is worse for the user than an honest error.
   - **Redelivery guard** (`ProcessedSubmissions`), deliberately best-effort and in-memory: re-running a submission is wasteful, not wrong, and the Api's consumer is the real idempotency gate.
   - `VerdictAggregator` decides the verdict from per-test outcomes — first failure in run order wins — so the rule is one pure, tested function rather than something each executor repeats.
   - **Messaging moved to `DsaPractice.Messaging`**, shared by both services: connection, publisher and the topology definition.
   - **Contracts now own their JSON settings** (`ContractJson`). The end-to-end run caught the verdict going over the wire as `0` instead of `"Accepted"`, which would have made inserting an enum member silently rewrite the meaning of every queued message.
   - **Nothing is executed yet:** `FakeSandboxExecutor` reports every test as passed and logs a warning saying so on every run. Item 11 replaces it.
10. **Api consumes `SubmissionJudged`** — the loop is closed: a submission goes to `Completed` with a verdict and per-test results, visible through `GET /api/v1/submissions/{id}`.
    - `SubmissionTestResults` table, plus `CompletedAtUtc` and `CompileOutput` on the submission.
    - **Idempotent by design, not by luck:** a submission that is already `Completed` is left untouched, because at-least-once delivery makes a duplicate result normal and a late one must not overwrite a verdict the user has already seen.
    - **Hidden test cases stay hidden**: the response says a hidden case failed, never what it printed — otherwise the hidden tests could be reconstructed one submission at a time.
    - Output is truncated to 4000 characters on the way in: submitted code decides that string's length, so the database must not.
    - A result for an unknown submission is acked and discarded (nothing to retry); a database failure is requeued.
    - Verified end to end on the running stack: submit → `Completed` with `"verdict": "Accepted"` and 5 results in about a second.
11. **`ISandboxExecutor` via Docker.DotNet** — one throwaway container per test case, never reused.
    - **Limits, all configurable** (proposed for review per the project skill): 1 CPU, the question's own memory limit with swap disabled, 64 pids, the question's time limit plus a 3s startup grace before the kill, 64 KB of captured output, a 32 MB tmpfs.
    - **Isolation:** no network at all, read-only root filesystem, every capability dropped, `no-new-privileges`, runs as `nobody`, writable space only in an in-memory tmpfs that dies with the container.
    - **Source arrives as a base64 environment variable** the container writes itself. Docker refuses to copy into a read-only rootfs, and a bind mount would expose a host path to submitted code (and wouldn't resolve at all once the Judge runs in a container against the host daemon).
    - **Timed by the container's own start/finish timestamps**, not the wall clock around the call: container startup costs hundreds of milliseconds and charging the submitter for the daemon's overhead fails correct solutions on a busy host. The wall clock still decides when to kill.
    - **Stops at the first failing test case** — the verdict is already decided, so the rest is spent sandbox time.
    - Switched to the maintained `Docker.DotNet.Enhanced` fork: Testcontainers already depends on it, and two packages producing `Docker.DotNet.dll` resolved to whichever NuGet picked.
12. **Python runner** — real execution is on by default: `python:3.12-alpine`, `TimeLimitMultiplier` 2.0 (a question's limit is written with a native-speed solution in mind, so an interpreter needs more of it for the same algorithm).
    - Verified against the committed reference solutions, not toy code: `two-sum/solutions/reference.py` is Accepted on all 5 tests, including the 50,000-element hidden one at ~150 ms.
    - Verified the failure paths end to end too: wrong output → `WrongAnswer`, an exception → `RuntimeError` carrying Python's traceback, an infinite loop → `TimeLimitExceeded`, and the brute-force O(n²) Two Sum → `TimeLimitExceeded` on exactly the large hidden test the content ships to reject it.
    - The tests read the Judge's **shipped** `appsettings.json`, so a broken runner configuration fails a test rather than only production.
13. **C# runner** — separate compile and run steps, each with its own limits; benchmark `dotnet run app.cs` (.NET 10 file-based apps) against invoking `csc` directly, keep the faster.
    *Learn:* compilation cost, image-size trade-offs, cold vs warm starts.
14. **Sandbox hardening + escape test suite** — `--network none`, read-only rootfs + small tmpfs, `cap-drop ALL`, `no-new-privileges`, pids limit, non-root user, default seccomp profile; the Judge reaches Docker through a restricted socket proxy instead of the raw, root-equivalent `docker.sock`; evaluate gVisor (`runsc`). Integration tests submit hostile code: fork bomb, infinite loop, 10 GB allocation, outbound network call, writes outside `/tmp`, output flood.
    *Learn:* Linux isolation primitives, defense in depth, threat modelling — a strong interview topic.

### Phase 2 — Frontend (local)
15. **Scaffold** (D10) — Vite + React + TS + React Router v7 + TanStack Query + Tailwind; typed API client generated from the Api's OpenAPI document; CORS on the Api for the dev origin.
16. **Question list + question page** — filter by difficulty/tag, rendered markdown statement, sample tests; question pages prerendered at build time.
17. **Editor, submit, verdict** — Monaco, language picker, submit, poll until a final verdict, per-test-case results (hidden tests show pass/fail only).
18. **Playwright E2E suite** — browse → open → submit → verdict, run in CI. A permanent suite, separate from the ad-hoc PR-demo recorder in the `git-workflow` skill.

### Phase 3 — Identity & accounts
19. **`Users` table + Api auth** (D3, D4) — `Users(Id, Issuer, Subject, DisplayName, Role, CreatedAt)`, just-in-time provisioning on a user's first authenticated request; `Submission.UserId` becomes a `Guid` foreign key; PR #8's endpoint protection and ownership filter return; local tokens via `dotnet user-jwts`, integration tests sign tokens with a test-only key.
    *Learn:* resource-server pattern, claims, `IClaimsTransformation`, resource-based authorization.
20. **Real IdP + SPA login** — choose the IdP after a fresh free-tier check (Microsoft Entra External ID, Auth0, Clerk, self-hosted Keycloak); Google + GitHub login first; the Api validates via `Authority` (JWKS, RS256); the SPA uses Authorization Code + PKCE with the access token held in memory. A BFF (tokens server-side, HttpOnly cookie) is the stricter option — revisit after launch.
    *Learn:* OIDC flows, PKCE, JWKS and key rotation, token lifetimes.
21. **My account** — my submission history; delete my account (local data + the IdP user).
    *Learn:* data-protection basics (GDPR, India's DPDP Act), hard vs soft delete.

### Phase 4 — Abuse protection & production readiness
A free code-execution service is an obvious target for crypto-mining and abuse — all of this lands before launch.

22. **Rate limits & backpressure** — ASP.NET Core rate limiter (per-user token bucket on submissions, per-IP on public reads), max source size, one in-flight submission per user, `503` + `Retry-After` when the judge queue is too deep.
    *Learn:* rate-limiting algorithms, backpressure, fail-fast vs queueing.
23. **Observability** — OpenTelemetry traces across Api → RabbitMQ → Judge (trace context in message headers), metrics (queue depth, judge duration, verdict counts), liveness/readiness health checks; Aspire dashboard locally, Grafana Cloud free tier in production.
    *Learn:* distributed tracing, context propagation, RED metrics.
24. **Production configuration** — per-environment settings, secrets via environment/files on the VM, forwarded headers behind Cloudflare, HSTS and security headers, CORS locked to the real domain, tests proving Development-only surfaces (Scalar, OpenAPI) are off elsewhere.

### Phase 5 — Go live (free hosting)
25. **VM provisioning** (D1) — Oracle Always Free arm64 VM, Docker, firewall with no inbound except key-only SSH, unattended security upgrades; check Oracle's current idle-instance reclamation policy first. Documented in `docs/`.
26. **Domain + Cloudflare** (D2) — DNS, Tunnel → `api.<domain>`, Pages → frontend, TLS; Turnstile on signup if the IdP doesn't already cover bots.
27. **Continuous deployment** — extend CI: multi-arch images → GHCR (free), a deploy job runs `docker compose pull && docker compose up -d` on the VM (migrator first); rollback = redeploy the previous image tag.
28. **Backups** — nightly `pg_dump` → Cloudflare R2 (10 GB free), a retention policy, and one real restore drill.
29. **Launch checklist** — 20–30 questions seeded, privacy policy, terms + acceptable-use policy (no mining, no attacks), about/contact pages, free uptime monitor, a load smoke test against the VM.

### Phase 6 — Growth & monetization (after launch)
30. **SEO** — `sitemap.xml`, `robots.txt`, canonical slug URLs, meta/Open Graph tags; register with Google Search Console.
31. **Content** — a written editorial for every question (the original content AdSense reviews), growing past 50 questions.
32. **Analytics** — Cloudflare Web Analytics (free and cookieless, so no consent banner needed for it).
33. **Google AdSense** — apply once content and traffic exist; `ads.txt`; Google-certified consent banner (required for EEA/UK/Swiss visitors); ads on list and editorial pages only, **never** on the editor/judge page.
34. **v2 candidates** — only once v1 has real users: progress tracking and streaks, more languages (Java, C++, JavaScript), leaderboard, SSE live verdicts, an admin UI for authoring questions, runtime/memory stats, BFF auth.

## Local dev
```bash
cp .env.example .env                    # one-time: local Postgres + RabbitMQ creds for docker-compose (gitignored)

# NOTE if you already had a RabbitMQ volume from before item 7: the broker only creates the
# configured user when its data directory is first initialised, and the built-in "guest" user
# can't connect from another container. Drop the old volume once (it holds no data worth
# keeping): docker compose --profile full-stack down && docker volume rm dsa-practice-platform_rabbitmqdata

# one-time: local Postgres connection string, kept out of source control via dotnet user-secrets
# (shared UserSecretsId between DsaPractice.Api and the migrations project — set once, both see it)
dotnet user-secrets set "ConnectionStrings:DsaPractice" \
  "Host=localhost;Port=5432;Database=dsapractice;Username=dsapractice;Password=<your .env password>" \
  --project source/DsaPractice.Api

# one-time: broker URI, also kept out of source control (it carries credentials).
# Use the RABBITMQ_USER/RABBITMQ_PASSWORD you put in .env.
dotnet user-secrets set "RabbitMq:Uri" \
  "amqp://dsapractice:<your .env password>@localhost:5672" \
  --project source/DsaPractice.Api

docker compose up -d    # Postgres + RabbitMQ, and a one-shot "migrator" that applies pending
                         # EF Core migrations, seeds content/questions/**, then exits -- no
                         # manual `dotnet ef` step needed. Re-run `docker compose run --rm
                         # migrator` after pulling a new migration or editing content; both
                         # halves are idempotent, so re-running costs nothing.
dotnet run --project source/DsaPractice.Api
dotnet run --project source/DsaPractice.Judge
```

`docker compose --profile full-stack up -d --build` additionally builds and runs `api`/`judge`
themselves in containers (`http://localhost:8080/scalar`) instead of via `dotnet run` — useful
for testing the actual Docker images, not needed for day-to-day local dev.

Tear it down with the same flag: `docker compose --profile full-stack down`. A plain
`docker compose down` only sees services without a profile, so it leaves `api`/`judge` running
and fails with "Network dsa-practice-platform_default — Resource is still in use". The
`--profile` flag is harmless when those services aren't running, so it's safe to always use it.

## Authoring a question

Questions are content, not code or data migrations (decision D9). One folder per question, named
with the slug that becomes its URL (`/problems/two-sum`):

```
content/questions/two-sum/
├── question.json            # title, difficulty (Easy|Medium|Hard), tags, timeLimitMs, memoryLimitMb
├── statement.md             # markdown problem statement
├── tests/
│   ├── sample/01.in 01.out  # shown to the user via GET /api/v1/questions/{slug}
│   └── hidden/01.in 01.out  # judged against, never returned by the Api
└── solutions/reference.py   # ignored by the seeder; a known-good solution to check tests against
```

Then apply it:

```bash
docker compose run --rm migrator          # upserts by slug; unchanged content writes nothing
```

Notes:
- **Ordinals are assigned by the loader** — samples first, then hidden, each in filename order. Don't
  number across the two folders yourself.
- **Every `.in` needs a matching `.out`**, and every question needs at least one sample. Broken content
  fails the run with every problem listed at once, before anything is written.
- **Editing is safe**: questions and test cases are matched by slug and ordinal and updated in place, so
  ids survive and existing submissions keep pointing at the right rows. Deleting a folder does *not*
  delete the question — remove it deliberately in SQL if you really mean to.
- Test case files are normalised (CRLF → LF, trailing newline trimmed) so a Windows checkout feeds the
  sandbox exactly what a Linux one does.
- Keep expected outputs honest: run `solutions/reference.py` against every `.in` before committing.

## Run and debug in VS Code

Prerequisites: the one-time `user-secrets`/`.env` setup above, and `docker compose up -d` running
(Postgres + the migrator — see "Local dev").

- Open the Run and Debug panel (`Ctrl+Shift+D` / `Cmd+Shift+D`), pick **DsaPractice.Api** or
  **DsaPractice.Judge** from the dropdown, press the green play button (or `F5`).
- `.vscode/launch.json` builds the selected project first (via `.vscode/tasks.json`), then launches
  it with the debugger attached — breakpoints, step-through, the works.
- For the Api, once you see `Now listening on: http://localhost:51942` in the Debug Console, VS
  Code opens `http://localhost:51942/scalar` automatically.
- Both configs are independent — running one doesn't start the other. To exercise the full
  submission flow end to end you'd eventually run both, same as the two `dotnet run` commands
  above.

## Conventions
Follows the user's standard `dotnet-production-code`, `dotnet-testing`, `react-frontend`, `git-workflow` skills, plus the project-specific `dsa-practice-platform` skill for the Judge architecture and MVP scope boundaries. Read the project skill before extending scope past v1 (more languages, leaderboard, etc.) — it's intentionally capped for now.
