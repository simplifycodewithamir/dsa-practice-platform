---
name: dsa-practice-platform
description: Use this skill for any work on the dsa-practice-platform repo — a free DSA question/judging platform for students. Covers the project's specific architecture (Judge microservice, sandboxed code execution, submission flow) on top of the standard dotnet-stack conventions. Trigger for "add a question", "build the judge service", "add a language runner", or any feature work in this repo.
---

# DSA Practice Platform — Project Conventions

Personal side-project, MVP-first. Defers the full enterprise CI/CD pattern (see deviation note below) — everything else follows `dotnet-production-code`, `dotnet-testing`, `react-frontend`, `git-workflow` as normal.

## Architecture
- **DsaPractice.Api** — Minimal API. Owns Questions, Users, Submissions (metadata only — never executes code itself).
- **DsaPractice.Judge** — Worker service. Consumes `submission.judge-requested` from RabbitMQ, runs the code in an ephemeral, resource-capped Docker container, publishes `submission.judged` with verdict + per-test-case results.
- **DsaPractice.Contracts** — Shared message/DTO contracts referenced by both Api and Judge (message queue payloads only — no shared business logic).
- **DsaPractice.Api.DataAccess** / **DsaPractice.Api.DataMigrations** — per `dotnet-production-code` convention, EF Core + migrations split from the API project.

## MVP scope (v1) — don't build past this without discussing
- 1–2 supported languages only (C# and Python to start)
- 20–30 hand-curated questions, seeded via migration, not an admin UI yet
- Pass/fail against test cases only — no runtime/memory complexity scoring yet
- No leaderboard, no user progress tracking yet — those are v2+

## Judge execution — hard rules
- **Never execute submitted code in-process or on the Api container.** Always via the Judge service's ephemeral Docker sandbox.
- Every sandbox run gets a hard CPU/memory/wall-clock limit (propose exact values per language runner — flag for review, don't hardcode silently).
- Sandbox containers are torn down immediately after each run — never reused across submissions.
- Judge → Api result delivery is async via RabbitMQ, never a synchronous HTTP callback.

## CI/CD deviation from `cicd-pipeline` (flagged explicitly, per convention)
This is a solo side-project — the full self-hosted Artifactory + separate `k8s-deploy` GitOps + Argo CD pattern is deferred until/unless this gets real usage. Day-1 CI here is a single GitHub Actions workflow per repo: restore → build → test → CodeQL → build Docker image. No deploy-repo split yet. Revisit and adopt the full `cicd-pipeline` pattern if this ever needs prod-grade rollout.

## Don't
- Don't let the Api project reference Docker/sandbox execution logic directly — that's Judge's job only.
- Don't skip the CPU/memory/time limits on any language runner "just for now."
- Don't add more than 2 languages or remove the 20-30 question cap until v1 is genuinely working end-to-end.
