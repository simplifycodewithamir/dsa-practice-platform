# DSA Practice Platform

Free DSA question-practice platform, deployed for students. MVP-scoped, sequenced project — see `.claude/skills/dsa-practice-platform/SKILL.md` for architecture and conventions before making changes, and `CLAUDE.md` for always-on repo context (stack, run/build commands).

## Structure
```
source-code/
├── CLAUDE.md                             # always-on project context
├── .claude/skills/dsa-practice-platform/SKILL.md   # Judge/sandbox architecture, loads on-demand
├── DsaPractice.sln
├── source/
│   ├── DsaPractice.Api/                  # Minimal API — Questions, Submissions (metadata only)
│   ├── DsaPractice.Api.DataAccess/       # EF Core, Postgres
│   ├── DsaPractice.Api.DataMigrations/   # migrations, split per convention
│   ├── DsaPractice.Judge/                # Worker service — sandboxed code execution
│   └── DsaPractice.Contracts/            # shared RabbitMQ message DTOs
├── tests/
│   ├── DsaPractice.Api.UnitTests/
│   ├── DsaPractice.Api.IntegrationTests/
│   └── DsaPractice.Judge.UnitTests/
├── frontend/                             # React + TypeScript (not yet scaffolded)
├── .github/workflows/ci.yml
└── docker-compose.yml                    # Postgres + RabbitMQ + both services, local dev
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

## What's NOT built yet — pick up here
4. Implement API authorization using latest oauth and JWT - use policy or filter based on best use case
5. **RabbitMQ publisher in Api** (publish `SubmissionJudgeRequested` on submission create)
6. **RabbitMQ consumer in Judge's `Worker.cs`** (currently just logs and idles)
7. **`ISandboxExecutor`** — the actual Docker.DotNet sandboxing logic (ephemeral container per run, CPU/memory/time limits — see `dsa-practice-platform` skill's hard rules on this)
8. **Per-language `ICodeRunner`** — start with C# and Python only (v1 scope)
9. **Seed migration** with 20-30 hand-written questions
10. **Frontend** — not scaffolded yet; React + TypeScript + Monaco Editor for the code input
11. **Playwright UI test suite** — once the frontend (item 9) exists, add a Playwright end-to-end test project exercising the real UI flows (browse questions → open one → submit code → see verdict). This is a permanent, CI-run suite, separate from the ad-hoc PR-demo recording tooling referenced in the `git-workflow` skill, which captures a one-off video for a PR and isn't checked into any repo here.

## Local dev
```bash
cp .env.example .env                    # one-time: local Postgres creds for docker-compose (gitignored)

# one-time: local Postgres connection string, kept out of source control via dotnet user-secrets
# (shared UserSecretsId between DsaPractice.Api and the migrations project — set once, both see it)
dotnet user-secrets set "ConnectionStrings:DsaPractice" \
  "Host=localhost;Port=5432;Database=dsapractice;Username=dsapractice;Password=<your .env password>" \
  --project source/DsaPractice.Api

docker compose up -d    # Postgres + RabbitMQ, and a one-shot "migrator" that applies
                         # pending EF Core migrations then exits -- no manual `dotnet ef`
                         # step needed. Re-run `docker compose run --rm migrator` any time
                         # you just want the schema brought up to date on its own (e.g.
                         # after pulling a new migration).
dotnet run --project source/DsaPractice.Api
dotnet run --project source/DsaPractice.Judge
```

`docker compose --profile full-stack up -d --build` additionally builds and runs `api`/`judge`
themselves in containers (`http://localhost:8080/scalar`) instead of via `dotnet run` — useful
for testing the actual Docker images, not needed for day-to-day local dev.

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
