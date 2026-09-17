# High-level design

## What this is

A free platform where students practise data-structures and algorithms questions: read a problem,
write a solution in the browser, submit it, and get it judged against test cases within seconds.

Judging is **stdin/stdout**, Codeforces-style, not LeetCode-style function signatures (decision D8).
A submission is a whole program: it reads its input from standard input and prints its answer to
standard output. Since item 19a each question ships a **starter skeleton per language** that already
contains the parsing, so a solver writes the algorithm rather than the I/O.

## The one problem that shapes everything

**Anyone on the internet can make this system execute code they wrote.**

That single fact drives most of the architecture. It is why execution lives in its own process that
the Api cannot reach into, why that process talks to the database not at all, why every run happens
in a container that is destroyed immediately afterwards, and why the request to run code travels as
a message rather than a function call. Remove that constraint and a much simpler design would do.

## Goals and non-goals

| Goal | Consequence in the design |
|---|---|
| Untrusted code never touches the Api or the host | Separate `DsaPractice.Judge` process; ephemeral, capability-stripped containers; no network in the sandbox |
| A submission is never silently lost | Transactional outbox (D6): the row and the message to publish commit together |
| A judged result is never applied twice | Idempotent consumer: a `Completed` submission ignores a duplicate result |
| Hidden test cases stay hidden | Filtered out of the read API; per-test output withheld for hidden cases even in results |
| Free to run | Single small VM, free tiers only (D1, D2); no managed queue, no managed database |
| A student sees a verdict in seconds | Polling, not sockets (D11) — a judge run takes seconds, so a socket earns nothing yet |

**Non-goals for v1**, all deliberate: no leaderboard, no progress tracking, no more than two
languages, no admin UI, no runtime/memory scoring. The project skill caps these; see the roadmap's
item 35 for what is queued behind real users.

## System context

Who and what the platform touches.

```mermaid
graph TB
    student(["Student<br/><i>writes and submits solutions</i>"]):::client
    author(["Content author<br/><i>adds questions as files</i>"]):::client
    admin(["Admin<br/><i>may read any submission</i>"]):::client

    platform["<b>DSA Practice Platform</b><br/>questions, submissions, judging"]:::api

    idp["Identity provider<br/><i>OIDC — planned, item 20</i>"]:::infra
    docker["Docker daemon<br/><i>creates sandbox containers</i>"]:::infra
    cdn["Cloudflare<br/><i>DNS, TLS, Pages — planned</i>"]:::infra

    student -->|"browses, submits, polls"| platform
    admin -->|"reads any submission"| platform
    author -->|"commits content/questions/**"| platform
    platform -.->|"validates bearer tokens"| idp
    platform -->|"runs untrusted code"| docker
    cdn -.->|"fronts"| platform

    classDef client fill:#e0e7ff,stroke:#4338ca,stroke-width:2px,color:#1e1b4b
    classDef api fill:#dbeafe,stroke:#1d4ed8,stroke-width:2px,color:#172554
    classDef infra fill:#e2e8f0,stroke:#475569,stroke-width:2px,color:#0f172a
```

Dashed edges are configured but not yet active: the Api validates tokens today, but nothing issues
them until item 20, and enforcement is off behind `Auth:RequireAuthentication`.

## Containers — the runtime pieces

```mermaid
graph TB
    subgraph browser["Browser"]
        spa["React SPA<br/>Vite · React Router · TanStack Query · Monaco"]:::client
    end

    subgraph host["Host — one VM, or one dev machine"]
        api["<b>DsaPractice.Api</b><br/>ASP.NET Core Minimal API<br/><i>never executes submitted code</i>"]:::api
        judge["<b>DsaPractice.Judge</b><br/>Worker Service<br/><i>the only place code runs</i>"]:::judge
        migrator["<b>migrator</b><br/>one-shot console app<br/>migrations + content seeding"]:::infra

        broker["<b>RabbitMQ</b><br/>exchange dsa.submissions<br/>+ dead-letter exchange"]:::broker
        db[("<b>Postgres</b><br/>questions, submissions,<br/>users, outbox")]:::store
        proxy["docker-socket-proxy<br/><i>narrowed Docker API</i>"]:::infra
        sandbox["ephemeral sandbox<br/>one container per test case"]:::sandbox
    end

    spa -->|"HTTPS · /api/v1"| api
    api -->|"EF Core"| db
    migrator -->|"migrate + seed"| db
    api -->|"publish submission.judge-requested"| broker
    broker -->|"consume"| judge
    judge -->|"publish submission.judged"| broker
    broker -->|"consume"| api
    judge -->|"Docker API"| proxy
    proxy -->|"create / start / remove"| sandbox

    classDef client fill:#e0e7ff,stroke:#4338ca,stroke-width:2px,color:#1e1b4b
    classDef api fill:#dbeafe,stroke:#1d4ed8,stroke-width:2px,color:#172554
    classDef judge fill:#f3e8ff,stroke:#7e22ce,stroke-width:2px,color:#3b0764
    classDef broker fill:#fef3c7,stroke:#b45309,stroke-width:2px,color:#451a03
    classDef store fill:#dcfce7,stroke:#15803d,stroke-width:2px,color:#052e16
    classDef sandbox fill:#ffe4e6,stroke:#be123c,stroke-width:2px,color:#4c0519
    classDef infra fill:#e2e8f0,stroke:#475569,stroke-width:2px,color:#0f172a
```

Note what is **missing** from that diagram: there is no arrow from the Judge to Postgres. That is
decision D7 and it is load-bearing — see [module interaction](07-module-interaction.md).

## The three flows

Everything the system does is one of these. Each is drawn step by step in
[sequence diagrams](04-sequence-diagrams.md).

```mermaid
graph LR
    subgraph read["1 · Read — synchronous"]
        r1["GET questions"]:::api --> r2[("Postgres")]:::store
    end

    subgraph write["2 · Submit — asynchronous"]
        w1["POST submission"]:::api --> w2["row + outbox<br/>one transaction"]:::store
        w2 --> w3["relay publishes"]:::broker
        w3 --> w4["Judge runs it"]:::judge
        w4 --> w5["result recorded"]:::store
    end

    subgraph content["3 · Content — build time"]
        c1["content/questions/**"]:::infra --> c2["migrator upserts by slug"]:::infra
        c2 --> c3[("Postgres")]:::store
    end

    classDef api fill:#dbeafe,stroke:#1d4ed8,stroke-width:2px,color:#172554
    classDef judge fill:#f3e8ff,stroke:#7e22ce,stroke-width:2px,color:#3b0764
    classDef broker fill:#fef3c7,stroke:#b45309,stroke-width:2px,color:#451a03
    classDef store fill:#dcfce7,stroke:#15803d,stroke-width:2px,color:#052e16
    classDef infra fill:#e2e8f0,stroke:#475569,stroke-width:2px,color:#0f172a
```

## Quality attributes, and how each is actually met

| Attribute | Mechanism | Where |
|---|---|---|
| **Durability** | Outbox row and submission commit in one transaction; durable exchange, durable queues, persistent messages, publisher confirms | `OutboxWriter`, `RabbitMqPublisher` |
| **Idempotency** | Judge skips a submission it already processed; Api ignores a result for an already-`Completed` submission | `ProcessedSubmissions`, `JudgedResultRecorder` |
| **Isolation** | No network, read-only root, all capabilities dropped, non-root user, pids/memory/CPU caps, tmpfs-only writes, container destroyed after each test case | `DockerSandboxExecutor` |
| **Fairness** | Per-run CPU quota and wall-clock limit; `TimeLimitMultiplier` per language so an interpreter is not judged at compiled speed | `SandboxOptions`, `LanguageRunner` |
| **Confidentiality** | Hidden test cases never leave the read API; hidden results report pass/fail and duration only | `QuestionsService`, `SubmissionsService` |
| **Availability** | A broker outage does not fail a submission — it queues; the Judge waits for the broker rather than failing to start | `OutboxRelay`, `ConnectWithRetryAsync` |
| **Observability** | Serilog structured logging throughout; OpenTelemetry is item 24 and not yet wired | `Program.cs` (both) |

## Consistency model, stated plainly

The system is **eventually consistent** between "submitted" and "judged", and it is
**at-least-once** end to end.

- The outbox publishes at least once: if the broker confirms but the relay crashes before its
  commit, the same message goes out again.
- The Judge acks only after publishing its result: if it dies mid-run, the broker redelivers.
- Therefore a submission **can** be judged twice, and a result **can** arrive twice.

Both duplicates are absorbed rather than prevented — by `ProcessedSubmissions` on the Judge and by
the `Completed` check in `JudgedResultRecorder`. Exactly-once delivery is not attempted, because
it is not achievable across a broker and a database without far more machinery than this earns.

## What is deliberately not built yet

| Not built | Roadmap |
|---|---|
| Real identity provider and SPA login | item 20 |
| Rate limiting and backpressure | item 22 |
| Shared consumer base class in `DsaPractice.Messaging` | item 23 |
| OpenTelemetry tracing across Api → broker → Judge | item 24 |
| Production configuration, hosting, CD, backups | items 25–29 |
| SSE live verdicts, more languages, leaderboard | item 35 |
