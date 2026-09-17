# Architecture and deployment

## Process view

Five processes in a full local stack, plus the sandbox containers the Judge creates and destroys.

```mermaid
graph TB
    subgraph procs["Long-running processes"]
        api["<b>DsaPractice.Api</b><br/>Kestrel :8080<br/>+ OutboxRelay (BackgroundService)<br/>+ JudgedResultConsumer (BackgroundService)"]:::api
        judge["<b>DsaPractice.Judge</b><br/>Generic Host, no listener<br/>+ JudgeRequestConsumer (BackgroundService)"]:::judge
        rabbit["<b>RabbitMQ</b> :5672<br/>management UI :15672"]:::broker
        pg[("<b>Postgres 16</b> :5432")]:::store
        proxy["<b>docker-socket-proxy</b> :2375<br/>CONTAINERS, IMAGES, VOLUMES, POST<br/>EXEC and BUILD off"]:::infra
    end

    subgraph oneshot["One-shot"]
        migrator["<b>migrator</b><br/>migrate → seed → exit 0"]:::infra
    end

    subgraph transient["Created and destroyed per run"]
        compile["compile container<br/><i>compiled languages only</i>"]:::sandbox
        run["run container<br/><i>one per test case</i>"]:::sandbox
    end

    api --> pg
    migrator --> pg
    api <--> rabbit
    judge <--> rabbit
    judge --> proxy
    proxy --> compile
    proxy --> run
    compile -.->|"artifacts via named volume"| run

    classDef api fill:#dbeafe,stroke:#1d4ed8,stroke-width:2px,color:#172554
    classDef judge fill:#f3e8ff,stroke:#7e22ce,stroke-width:2px,color:#3b0764
    classDef broker fill:#fef3c7,stroke:#b45309,stroke-width:2px,color:#451a03
    classDef store fill:#dcfce7,stroke:#15803d,stroke-width:2px,color:#052e16
    classDef sandbox fill:#ffe4e6,stroke:#be123c,stroke-width:2px,color:#4c0519
    classDef infra fill:#e2e8f0,stroke:#475569,stroke-width:2px,color:#0f172a
```

The Api hosts **three** things in one process: the HTTP pipeline, the outbox relay, and the result
consumer. The two background services are `BackgroundService` implementations registered with
`AddHostedService`, so they start with the host and stop with it.

## Why the Judge is a separate process

It is the single most important structural decision, so it is worth stating the alternatives that
were rejected.

| Option | Why not |
|---|---|
| Run submissions in the Api process | A sandbox escape, or merely a fork bomb, takes down the thing serving every other student |
| Run them in the Api container via the Docker socket | The Api would hold a root-equivalent handle to the host; a web-facing process should not |
| Synchronous HTTP call to a judging service | Judging takes seconds and can queue; HTTP would hold a request thread open and give no backpressure |

What is built instead: an asynchronous message, a separate process, and a Docker API reached through
a proxy that only exposes the endpoints the Judge actually uses.

## The Docker socket, and why a proxy

The Judge needs the Docker API to create sandboxes. The raw socket is root-equivalent on the host —
anything holding it can start a privileged container with the host filesystem bind-mounted and own
the machine. So the Judge never gets the socket. `tecnativa/docker-socket-proxy` holds it and
exposes a narrowed API:

```mermaid
graph LR
    judge["Judge<br/>DOCKER_HOST=tcp://docker-socket-proxy:2375"]:::judge
    proxy["docker-socket-proxy"]:::infra
    sock["/var/run/docker.sock<br/><i>mounted read-only</i>"]:::infra
    daemon["Docker daemon"]:::infra

    judge -->|"create · start · attach · wait<br/>inspect · remove · pull · volumes"| proxy
    proxy -->|"only those"| sock
    sock --> daemon

    classDef judge fill:#f3e8ff,stroke:#7e22ce,stroke-width:2px,color:#3b0764
    classDef infra fill:#e2e8f0,stroke:#475569,stroke-width:2px,color:#0f172a
```

`EXEC`, `BUILD`, `NETWORKS`, `SWARM`, `SYSTEM`, `SECRETS`, `CONFIGS`, `AUTH` are all **0**. A
compromised Judge cannot exec into a running container, build an image, or read a secret.

## Messaging topology

Declared identically by both services on every connection, by `RabbitMqTopology.DeclareAsync`.
Declaration is idempotent, so whichever process starts first creates it — but RabbitMQ rejects a
redeclaration whose durability or arguments differ (406 `PRECONDITION_FAILED`), which is exactly
why this lives in one shared class rather than once per service.

```mermaid
graph LR
    apiPub["Api<br/>OutboxRelay"]:::api
    judgePub["Judge<br/>JudgeRequestConsumer"]:::judge

    ex{{"<b>dsa.submissions</b><br/>direct · durable"}}:::broker
    dlx{{"<b>dsa.submissions.dlx</b><br/>direct · durable"}}:::broker

    q1["submission.judge-requested<br/>durable"]:::broker
    q2["submission.judged<br/>durable"]:::broker
    dlq["submission.dead-letter<br/>durable"]:::broker

    apiPub -->|"rk: submission.judge-requested"| ex
    judgePub -->|"rk: submission.judged"| ex
    ex --> q1
    ex --> q2
    q1 -->|consume| judgePub
    q2 -->|consume| apiPub
    q1 -.->|"rejected"| dlx
    q2 -.->|"rejected"| dlx
    dlx --> dlq

    classDef api fill:#dbeafe,stroke:#1d4ed8,stroke-width:2px,color:#172554
    classDef judge fill:#f3e8ff,stroke:#7e22ce,stroke-width:2px,color:#3b0764
    classDef broker fill:#fef3c7,stroke:#b45309,stroke-width:2px,color:#451a03
```

Both work queues dead-letter to the same parking queue. A message only lands there when a consumer
**rejects** it outright — a payload that will not deserialise, or a failure it decided not to retry.
So anything in `submission.dead-letter` is a signal worth looking at, not routine overflow.

| Setting | Value | Why |
|---|---|---|
| Exchange type | direct, durable | One routing key per message type; survives a broker restart |
| Delivery mode | persistent | Pointless to have a durable queue and non-persistent messages |
| Publisher confirms | on, with tracking | The relay must not mark a row processed for a message the broker never took |
| `mandatory` | true | An unroutable message is a bug, not something to discard quietly |
| Judge prefetch | `Judge:Prefetch`, default 1 | Without it the broker pushes the whole queue at one consumer while others idle |
| Api prefetch | 10 | Recording a result is cheap; judging is not |
| Ack timing | after the result is published | Die mid-run and the broker redelivers rather than losing the submission |

## Local development topology

`docker-compose.yml` has two modes.

```mermaid
graph TB
    subgraph plain["docker compose up -d — the default"]
        d1["postgres"]:::store
        d2["rabbitmq"]:::broker
        d3["migrator (runs, exits)"]:::infra
        d4["Api and Judge: you run them<br/><i>dotnet run, or F5 in an IDE</i>"]:::api
    end

    subgraph full["--profile full-stack"]
        f1["postgres"]:::store
        f2["rabbitmq"]:::broker
        f3["migrator"]:::infra
        f4["api container :8080"]:::api
        f5["judge container"]:::judge
        f6["docker-socket-proxy"]:::infra
    end

    classDef api fill:#dbeafe,stroke:#1d4ed8,stroke-width:2px,color:#172554
    classDef judge fill:#f3e8ff,stroke:#7e22ce,stroke-width:2px,color:#3b0764
    classDef broker fill:#fef3c7,stroke:#b45309,stroke-width:2px,color:#451a03
    classDef store fill:#dcfce7,stroke:#15803d,stroke-width:2px,color:#052e16
    classDef infra fill:#e2e8f0,stroke:#475569,stroke-width:2px,color:#0f172a
```

The default brings up **infrastructure only**, so you run the two services yourself under a
debugger — that is the everyday loop, and [the debugging guide](../debugging.md) covers it.
`--profile full-stack` additionally builds and runs the app images, which is what you want when
testing the images themselves or running the e2e suite.

Tear down with the same flag: a plain `docker compose down` does not see profiled services, leaves
`api`/`judge` running, and then fails with *"Network … Resource is still in use"*.

## Configuration and secrets

| Setting | Source locally | Source in a deployment |
|---|---|---|
| `ConnectionStrings:DsaPractice` | `dotnet user-secrets` | environment |
| `RabbitMq:Uri` (carries credentials) | `dotnet user-secrets` | environment |
| Postgres/RabbitMQ credentials for compose | gitignored `.env` | n/a |
| `Authentication:Schemes:Bearer` | `dotnet user-jwts` (D4) | the identity provider's settings (item 20) |
| `Submissions:SupportedLanguages` | `appsettings.json` | same |
| `Judge:Sandbox:*` | `appsettings.json` | overridable per deployment |

Nothing that carries a credential is ever in `appsettings.json`. The Api and the migrations project
share one `UserSecretsId`, so `dotnet user-secrets set` against either supplies both.

Every options class is bound with `AddOptions<T>().Bind(...).Validate(...).ValidateOnStart()`, so a
misconfiguration fails at **startup** with a named error rather than at the first request.

## Planned production topology

Not built — items 26–29. Recorded here so the target is visible while reading the rest.

```mermaid
graph TB
    user(["Student"]):::client
    cf["Cloudflare<br/>DNS · TLS · Tunnel · Pages · R2"]:::infra

    subgraph vm["Oracle Always Free arm64 VM — no inbound ports open"]
        capi["api container"]:::api
        cjudge["judge container"]:::judge
        crabbit["rabbitmq"]:::broker
        cpg[("postgres")]:::store
    end

    r2[("R2<br/>nightly pg_dump")]:::store

    user --> cf
    cf -->|"Pages: static SPA"| user
    cf -->|"Tunnel → api.&lt;domain&gt;"| capi
    capi <--> crabbit
    cjudge <--> crabbit
    capi --> cpg
    cpg -.->|"backup"| r2

    classDef client fill:#e0e7ff,stroke:#4338ca,stroke-width:2px,color:#1e1b4b
    classDef api fill:#dbeafe,stroke:#1d4ed8,stroke-width:2px,color:#172554
    classDef judge fill:#f3e8ff,stroke:#7e22ce,stroke-width:2px,color:#3b0764
    classDef broker fill:#fef3c7,stroke:#b45309,stroke-width:2px,color:#451a03
    classDef store fill:#dcfce7,stroke:#15803d,stroke-width:2px,color:#052e16
    classDef infra fill:#e2e8f0,stroke:#475569,stroke-width:2px,color:#0f172a
```

The Tunnel is the point: the VM opens **no** inbound HTTP port, and there is no reverse proxy or
certificate renewal to operate.

## CI

`.github/workflows/dotnet.yml`, two jobs.

```mermaid
graph LR
    subgraph j1["build-and-test"]
        a1["restore"]:::infra --> a2["build Release"]:::infra
        a2 --> a3["unit tests<br/>Api · ContentSeeding · Judge"]:::infra
        a3 --> a4["frontend<br/>lint · vitest · build"]:::client
        a4 --> a5["integration tests<br/>Testcontainers"]:::infra
    end

    subgraph j2["end-to-end"]
        b1["compose --profile full-stack up --build"]:::infra --> b2["build SPA"]:::client
        b2 --> b3["playwright install"]:::infra
        b3 --> b4["npm run test:e2e"]:::client
    end

    classDef client fill:#e0e7ff,stroke:#4338ca,stroke-width:2px,color:#1e1b4b
    classDef infra fill:#e2e8f0,stroke:#475569,stroke-width:2px,color:#0f172a
```

Unit tests run before integration tests on purpose: fast feedback without waiting on Docker, and a
unit failure surfaces before paying for the Testcontainers run at all. The e2e suite is its own job
because it builds images and is slower than everything else combined.

> **Accuracy note.** The root README describes CI as "restore → build → test → CodeQL → Docker
> image". The workflow as committed has no CodeQL step and no image-publishing step — the e2e job
> builds images through compose but does not push them. Either the workflow or that sentence should
> change; this document describes the workflow.
