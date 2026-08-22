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
│   └── DsaPractice.Judge.Tests/
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

## What's NOT built yet — pick up here
1. **DbContext registration + first migration** (Api `Program.cs` has a TODO for this)
2. **Global exception handler → ProblemDetails mapping** (per `dotnet-production-code` skill)
3. **Questions/Submissions endpoint implementations** (currently stubs in `Endpoints/`)
4. **RabbitMQ publisher in Api** (publish `SubmissionJudgeRequested` on submission create)
5. **RabbitMQ consumer in Judge's `Worker.cs`** (currently just logs and idles)
6. **`ISandboxExecutor`** — the actual Docker.DotNet sandboxing logic (ephemeral container per run, CPU/memory/time limits — see `dsa-practice-platform` skill's hard rules on this)
7. **Per-language `ICodeRunner`** — start with C# and Python only (v1 scope)
8. **Seed migration** with 20-30 hand-written questions
9. **Frontend** — not scaffolded yet; React + TypeScript + Monaco Editor for the code input

## Local dev
```bash
cp .env.example .env                    # one-time: local Postgres creds for docker-compose (gitignored)

# one-time: local Postgres connection string, kept out of source control via dotnet user-secrets
# (shared UserSecretsId between DsaPractice.Api and the migrations project — set once, both see it)
dotnet user-secrets set "ConnectionStrings:DsaPractice" \
  "Host=localhost;Port=5432;Database=dsapractice;Username=dsapractice;Password=<your .env password>" \
  --project source/DsaPractice.Api

docker compose up postgres rabbitmq -d
dotnet run --project source/DsaPractice.Api
dotnet run --project source/DsaPractice.Judge
```

## Conventions
Follows the user's standard `dotnet-production-code`, `dotnet-testing`, `react-frontend`, `git-workflow` skills, plus the project-specific `dsa-practice-platform` skill for the Judge architecture and MVP scope boundaries. Read the project skill before extending scope past v1 (more languages, leaderboard, etc.) — it's intentionally capped for now.
