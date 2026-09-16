# Sequence diagrams

The interesting behaviour here is asynchronous, so these are the fastest route into the codebase.
Each diagram names the real types; find them in [class diagrams](05-class-diagrams.md).

## 1 · Submitting a solution — the happy path

End to end, from the click to the verdict on screen.

```mermaid
sequenceDiagram
    autonumber
    participant SPA as SubmitPanel (SPA)
    participant EP as SubmissionsEndpoints
    participant SVC as SubmissionsService
    participant DB as Postgres
    participant RLY as OutboxRelay
    participant MQ as RabbitMQ
    participant JC as JudgeRequestConsumer
    participant EX as DockerSandboxExecutor
    participant SBX as Sandbox container
    participant RC as JudgedResultRecorder

    SPA->>EP: POST /api/v1/submissions
    EP->>EP: FluentValidation (shape)
    EP->>SVC: CreateSubmissionAsync
    SVC->>DB: load Question + ALL test cases
    SVC->>DB: get or create Users row
    SVC->>SVC: build Submission (Pending)
    SVC->>SVC: OutboxWriter.Enqueue(same DbContext)

    rect rgb(220, 252, 231)
        Note over SVC,DB: ONE transaction — submission + message
        SVC->>DB: SaveChangesAsync
    end

    SVC-->>EP: SubmissionResponse
    EP-->>SPA: 201 Created

    Note over SPA: polls GET /submissions/{id} every 1s

    RLY->>DB: claim due rows (FOR UPDATE SKIP LOCKED)
    RLY->>MQ: publish submission.judge-requested
    MQ-->>RLY: publisher confirm
    RLY->>DB: ProcessedAtUtc = now

    MQ->>JC: deliver (prefetch 1, autoAck false)
    JC->>JC: ProcessedSubmissions.TryMarkProcessed
    JC->>EX: ExecuteAsync(request)

    loop each test case, in ordinal order
        EX->>SBX: create (no network, ro-rootfs, caps dropped)
        EX->>SBX: start + write stdin
        SBX-->>EX: stdout / stderr / exit code
        EX->>SBX: remove
        Note over EX: stop at the first failure
    end

    EX-->>JC: ExecutionOutcome
    JC->>JC: VerdictAggregator.Aggregate
    JC->>MQ: publish submission.judged
    JC->>MQ: BasicAck (only now)

    MQ->>RC: deliver
    RC->>DB: Completed + verdict + per-test rows
    RC->>MQ: BasicAck

    SPA->>EP: GET /submissions/{id}
    EP-->>SPA: Completed · Accepted
```

Two things to notice. The green band is the whole durability guarantee — one `SaveChanges`, not two
operations that can disagree. And the Judge's `BasicAck` is the **last** thing it does, after the
result is safely published.

## 2 · The broker is down

The case the outbox exists for. The student sees no difference.

```mermaid
sequenceDiagram
    autonumber
    participant SPA as SPA
    participant SVC as SubmissionsService
    participant DB as Postgres
    participant RLY as OutboxRelay
    participant MQ as RabbitMQ (down)

    SPA->>SVC: POST /submissions
    SVC->>DB: SaveChanges (submission + outbox row)
    SVC-->>SPA: 201 Created

    Note over SPA: 201, not 500 — nothing here touched the broker

    RLY->>MQ: publish (attempt 1)
    MQ--xRLY: connection refused
    RLY->>DB: AttemptCount=1, LastError, NextAttempt=+2s

    RLY->>MQ: publish (attempt 2)
    MQ--xRLY: connection refused
    RLY->>DB: AttemptCount=2, NextAttempt=+4s

    Note over RLY,MQ: exponential, doubling, capped at MaxRetryDelay

    MQ->>MQ: broker comes back
    RLY->>MQ: publish (attempt 3)
    MQ-->>RLY: confirm
    RLY->>DB: ProcessedAtUtc = now
```

Before item 8 this returned **500** for a submission that had in fact been saved. The behaviour above
was verified against a real outage, not reasoned about.

## 3 · Redelivery — the same submission judged twice

At-least-once is the contract, so this path must be harmless.

```mermaid
sequenceDiagram
    autonumber
    participant MQ as RabbitMQ
    participant JC as JudgeRequestConsumer
    participant PS as ProcessedSubmissions
    participant RC as JudgedResultRecorder
    participant DB as Postgres

    MQ->>JC: deliver submission S
    JC->>PS: TryMarkProcessed(S)
    PS-->>JC: true (first sight)
    JC->>JC: run, aggregate, publish
    JC->>MQ: ack

    Note over MQ,JC: relay republished S — it crashed before its commit

    MQ->>JC: deliver S again
    JC->>PS: TryMarkProcessed(S)
    PS-->>JC: false (already seen)
    JC->>MQ: ack without re-running

    Note over RC,DB: and if a duplicate *result* reaches the Api

    MQ->>RC: SubmissionJudged for S
    RC->>DB: load submission
    DB-->>RC: Status = Completed
    RC->>RC: RecordOutcome.AlreadyRecorded
    RC->>MQ: ack, database untouched
```

Two independent guards, because `ProcessedSubmissions` is a bounded **in-memory** cache on one Judge
instance: it does not survive a restart and does not span instances. The database check in
`JudgedResultRecorder` is the one that always holds.

## 4 · The Judge itself fails

Distinct from the submitted code failing. The student must not be left watching a spinner.

```mermaid
sequenceDiagram
    autonumber
    participant MQ as RabbitMQ
    participant JC as JudgeRequestConsumer
    participant EX as DockerSandboxExecutor
    participant DLQ as submission.dead-letter
    participant RC as JudgedResultRecorder

    MQ->>JC: deliver
    JC->>EX: ExecuteAsync
    EX--xJC: throws (daemon unreachable, image pull failed, ...)

    rect rgb(255, 228, 230)
        Note over JC: tell the Api anyway
        JC->>MQ: publish SubmissionJudged(InternalError, [])
        JC->>MQ: BasicReject(requeue: false)
        MQ->>DLQ: parked for a human
    end

    MQ->>RC: InternalError
    RC->>RC: Completed, verdict InternalError
    Note over RC: the SPA says this is not the submitter's fault
```

A submission stuck `Running` forever is worse for the student than an honest `InternalError`. The
original message is parked rather than retried, so the failure is visible instead of swallowed.

## 5 · A bad payload

```mermaid
sequenceDiagram
    autonumber
    participant MQ as RabbitMQ
    participant JC as JudgeRequestConsumer
    participant DLQ as submission.dead-letter

    MQ->>JC: deliver
    JC->>JC: JsonSerializer.Deserialize
    JC--xJC: JsonException
    JC->>MQ: BasicReject(requeue: false)
    MQ->>DLQ: parked

    Note over JC,DLQ: requeue would loop forever — nothing about<br/>this message improves by trying it again
```

The same applies to a message that deserialises but has no `SubmissionId`.

## 6 · Inside the sandbox — a compiled language

C# needs a compile step; Python does not. Compiling happens **once per submission**, not once per
test case.

```mermaid
sequenceDiagram
    autonumber
    participant EX as DockerSandboxExecutor
    participant D as Docker (via proxy)
    participant C as Compile container
    participant V as Artifact volume
    participant R as Run container

    EX->>D: ensure run image + compile image
    EX->>D: create volume (per submission)

    rect rgb(255, 228, 230)
        Note over EX,V: compile once
        EX->>C: create (root, no network, ro-rootfs, caps dropped)
        EX->>C: start — source via base64 env var
        C->>V: write artifacts to /out
        C-->>EX: exit code + compiler output
    end

    alt compile failed
        EX-->>EX: ExecutionOutcome(CompilationFailed: true)
        Note over EX: no per-test results — nothing ran
    else compiled
        loop each test case until one fails
            EX->>R: create (user 65534, /out mounted READ-ONLY)
            EX->>R: start, write stdin, read stdout/stderr
            R-->>EX: output + exit code
            EX->>D: inspect (OOMKilled? container-measured duration?)
            EX->>D: remove container
        end
    end

    EX->>D: remove volume
```

Why the compile container runs as **root** when run containers never do: a Docker volume is created
root-owned, so nothing else could write artifacts into it. The trade-off is narrow and deliberate —
that container has no network, no capabilities and a read-only root filesystem, and it runs a
compiler, not the submission. The submission only ever *runs* as `65534:65534`, with `/out` mounted
read-only.

## 7 · How a verdict is decided for one test case

```mermaid
flowchart TD
    start(["container finished"]) --> oom{"State.OOMKilled?"}
    oom -->|yes| mle["MemoryLimitExceeded"]:::bad
    oom -->|no| slow{"elapsed > time limit<br/>× TimeLimitMultiplier?"}
    slow -->|yes| tle["TimeLimitExceeded"]:::bad
    slow -->|no| exit{"exit code == 0?"}
    exit -->|no| rte["RuntimeError<br/><i>stderr returned</i>"]:::bad
    exit -->|yes| cmp{"OutputComparer.Matches?"}
    cmp -->|no| wa["WrongAnswer<br/><i>actual output returned</i>"]:::bad
    cmp -->|yes| ok["Passed"]:::good

    classDef bad fill:#ffe4e6,stroke:#be123c,stroke-width:2px,color:#4c0519
    classDef good fill:#dcfce7,stroke:#15803d,stroke-width:2px,color:#052e16
```

There is also an earlier exit: if reading the container's output exceeds *time limit + startup
grace*, the read is cancelled and the case is `TimeLimitExceeded` without waiting for the container
at all.

Duration is measured from the **container's own** start and finish times, not the wall clock around
the whole call. Creating and starting a container costs hundreds of milliseconds, and charging the
submitter for the daemon's overhead would fail correct solutions on a busy host. The wall clock still
governs when the container is killed.

## 8 · Just-in-time user provisioning

```mermaid
sequenceDiagram
    autonumber
    participant SPA as SPA
    participant MW as JwtBearer middleware
    participant CUP as CurrentUserProvider
    participant DB as Postgres

    SPA->>MW: POST /submissions (Authorization: Bearer …)
    MW->>MW: validate signature, issuer, lifetime
    MW-->>CUP: ClaimsPrincipal

    CUP->>CUP: read iss, sub, name

    alt not authenticated
        Note over CUP: only while Auth:RequireAuthentication is false
        CUP->>CUP: (LocalIssuer, "anonymous")
    end

    CUP->>DB: SELECT … WHERE Issuer=@i AND Subject=@s

    alt found
        DB-->>CUP: existing user
    else first sight
        CUP->>DB: INSERT Users (Role = User)
        Note over CUP,DB: no registration form — the provider already did that
    end

    CUP-->>SPA: submission owned by that internal Guid
```

The submission stores the **internal** `Guid`, never the provider's `sub` (D3). Switching identity
provider therefore cannot orphan anyone's history.

## 9 · Applying content

```mermaid
sequenceDiagram
    autonumber
    participant CLI as docker compose run --rm migrator
    participant P as Program
    participant L as ContentLoader
    participant S as QuestionSeeder
    participant DB as Postgres

    CLI->>P: start
    P->>DB: Migrate() — apply pending migrations
    P->>L: Load(contentRoot)

    loop each content/questions/<slug>/
        L->>L: question.json · statement.md · starters/ · tests/
        L->>L: collect problems, do not throw yet
    end

    alt any problem
        L--xP: ContentException listing EVERY problem at once
        P-->>CLI: non-zero exit, nothing written
    else valid
        L-->>S: IReadOnlyList<QuestionContent>
        S->>DB: load existing by slug (with test cases)
        S->>S: insert new · update in place · reconcile by ordinal
        S->>DB: one SaveChanges for the whole run
        S-->>CLI: "N created, N updated, N unchanged"
    end
```

Reporting every broken question at once is deliberate: a seeder that fails on the first problem, is
fixed, then fails on the next is a slow way to find out the content is wrong.

## 10 · The SPA's read and poll cycle

```mermaid
sequenceDiagram
    autonumber
    participant U as Student
    participant QP as QuestionPage
    participant RQ as TanStack Query
    participant API as Api

    U->>QP: open /problems/two-sum
    QP->>RQ: useQuestion(slug)
    RQ->>API: GET /api/v1/questions/two-sum
    API-->>RQ: statement, samples, limits, starters
    QP->>QP: SubmitPanel opens with starters[language]

    U->>QP: edit, press Submit
    QP->>RQ: useCreateSubmission()
    RQ->>API: POST /api/v1/submissions
    API-->>RQ: 201 { id, status: Pending }

    loop every 1s while status != Completed
        RQ->>API: GET /api/v1/submissions/{id}
        API-->>RQ: Pending
    end

    RQ->>API: GET /api/v1/submissions/{id}
    API-->>RQ: Completed · Accepted · per-test results
    RQ->>QP: refetchInterval → false
    QP->>U: VerdictPanel
```

`refetchInterval` returning `false` on `Completed` is what stops the poll — decision D11, and the
reason an idle question page makes no requests at all.
