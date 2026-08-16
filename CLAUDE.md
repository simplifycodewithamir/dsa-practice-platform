# DSA Practice Platform

Free DSA question-practice platform for students. Personal side-project, MVP-first — see `.claude/skills/dsa-practice-platform/SKILL.md` for the Judge/sandbox architecture rules, which load only when relevant. This file is always-on context for anything in this repo.

## Stack
- Backend: ASP.NET Core (.NET 10), Minimal APIs, EF Core + PostgreSQL
- Messaging: RabbitMQ (Api → Judge submission flow)
- Judge sandboxing: Docker.DotNet, ephemeral containers per code run
- Frontend: React + TypeScript (not yet scaffolded), Monaco Editor planned for code input
- Tests: xUnit, Moq, Testcontainers

## Solution layout
```
DsaPractice.sln
source/
  DsaPractice.Api/                  # Minimal API — Questions, Submissions (metadata only, never executes code)
  DsaPractice.Api.DataAccess/       # EF Core, Postgres
  DsaPractice.Api.DataMigrations/   # migrations, split per convention
  DsaPractice.Judge/                # Worker service — sandboxed code execution
  DsaPractice.Contracts/            # shared RabbitMQ message DTOs
tests/
  DsaPractice.Api.Tests/
  DsaPractice.Judge.Tests/
frontend/                           # React + TypeScript
```

## Run locally
```bash
docker compose up postgres rabbitmq -d
dotnet run --project source/DsaPractice.Api
dotnet run --project source/DsaPractice.Judge
```

## Build & test
```bash
dotnet restore DsaPractice.sln
dotnet build DsaPractice.sln
dotnet test DsaPractice.sln
```

## Conventions
This project follows the general-purpose skills in `~/.claude/skills/` — `dotnet-production-code`, `dotnet-testing`, `react-frontend`, `git-workflow` — for everything not specific to this repo. The one deviation: day-1 CI here is a single lightweight GitHub Actions workflow (restore → build → test → CodeQL → Docker image), not the full Artifactory/Argo CD/k8s-deploy pattern from `cicd-pipeline` — this is a solo project, revisit that pattern only if it needs real prod-grade rollout later.

## Current status
Skeleton scaffolded — solution, all csproj files, entity/contract skeletons, Program.cs stubs with TODOs, day-1 CI, docker-compose for local dev. See README.md for the numbered "what's not built yet" list.
