# Class diagrams

Every type here exists at the path given. Visibility follows the project convention: `internal`
unless something outside the assembly must see it, and only `Program` is public in the Api.

## DsaPractice.Api — HTTP layer

Endpoints are thin adapters: parse, call a service, map to `Results.*`. They never touch
`DbContext`.

```mermaid
classDiagram
    class QuestionsEndpoints {
        <<static>>
        +MapQuestionsEndpoints(RouteGroupBuilder) RouteGroupBuilder
        -GetQuestions(IQuestionsService, CancellationToken)
        -GetQuestionBySlug(string, IQuestionsService, CancellationToken)
        -SlugRouteParameter : string
    }
    class SubmissionsEndpoints {
        <<static>>
        +MapSubmissionsEndpoints(RouteGroupBuilder) RouteGroupBuilder
        -CreateSubmission(CreateSubmissionRequest, IValidator, ISubmissionsService, CancellationToken)
        -GetSubmissionById(Guid, ISubmissionsService, CancellationToken)
    }
    class IQuestionsService {
        <<interface>>
        +GetQuestionsAsync(CancellationToken) IReadOnlyList~QuestionSummaryResponse~
        +GetQuestionBySlugAsync(string, CancellationToken) QuestionDetailResponse
    }
    class ISubmissionsService {
        <<interface>>
        +CreateSubmissionAsync(CreateSubmissionRequest, CancellationToken) SubmissionResponse
        +GetSubmissionByIdAsync(Guid, CancellationToken) SubmissionResponse
    }
    class QuestionsService {
        -db : DsaPracticeDbContext
    }
    class SubmissionsService {
        -db : DsaPracticeDbContext
        -timeProvider : TimeProvider
        -outboxWriter : IOutboxWriter
        -currentUserProvider : ICurrentUserProvider
    }
    class CreateSubmissionRequestValidator {
        <<AbstractValidator>>
        +ctor(IOptionsSnapshot~SubmissionsOptions~)
    }
    class SubmissionOwnershipFilter {
        <<IEndpointFilter>>
        +InvokeAsync(EndpointFilterInvocationContext, EndpointFilterDelegate)
    }

    QuestionsEndpoints ..> IQuestionsService
    SubmissionsEndpoints ..> ISubmissionsService
    SubmissionsEndpoints ..> CreateSubmissionRequestValidator
    SubmissionsEndpoints ..> SubmissionOwnershipFilter
    IQuestionsService <|.. QuestionsService
    ISubmissionsService <|.. SubmissionsService
```

`SubmissionsService` shows the convention for a business service: `DbContext` injected directly
(Scoped, matching `DbContext`'s own lifetime), guard clauses that throw first, happy path last. No
repository — a blind wrapper over `DbSet<T>` re-exposes its surface for no gain.

## DsaPractice.Api — DTOs

Records, not classes. Immutable by default.

```mermaid
classDiagram
    class QuestionSummaryResponse {
        <<record>>
        +Guid Id
        +string Slug
        +string Title
        +QuestionDifficulty Difficulty
        +IReadOnlyList~string~ Tags
    }
    class QuestionDetailResponse {
        <<record>>
        +Guid Id
        +string Slug
        +string Title
        +string Description
        +QuestionDifficulty Difficulty
        +IReadOnlyList~string~ Tags
        +int TimeLimitMs
        +int MemoryLimitMb
        +IReadOnlyDictionary~string,string~ Starters
        +IReadOnlyList~TestCaseResponse~ SampleTestCases
        +FromEntity(Question)$ QuestionDetailResponse
    }
    class TestCaseResponse {
        <<record>>
        +Guid Id
        +int Ordinal
        +string Input
        +string ExpectedOutput
    }
    class CreateSubmissionRequest {
        <<record>>
        +Guid QuestionId
        +string Language
        +string SourceCode
    }
    class SubmissionResponse {
        <<record>>
        +Guid Id
        +Guid QuestionId
        +Guid UserId
        +string Language
        +SubmissionStatus Status
        +SubmissionVerdict? Verdict
        +DateTimeOffset SubmittedAtUtc
        +DateTimeOffset? CompletedAtUtc
        +string? CompileOutput
        +IReadOnlyList~SubmissionTestResultResponse~? TestResults
        +FromEntity(Submission)$ SubmissionResponse
    }
    class SubmissionTestResultResponse {
        <<record>>
        +int Ordinal
        +bool IsHidden
        +bool Passed
        +long ExecutionTimeMs
        +string? ActualOutput
        +string? ErrorMessage
    }

    QuestionDetailResponse *-- TestCaseResponse
    SubmissionResponse *-- SubmissionTestResultResponse
```

`CreateSubmissionRequest` has **no `UserId`** — that is the design, not an omission.
`SubmissionTestResultResponse` carries `ActualOutput` and `ErrorMessage` as null for a hidden case:
the student learns that it failed, never what it contained.

## DsaPractice.Api — outbox and result consumer

```mermaid
classDiagram
    class IOutboxWriter {
        <<interface>>
        +Enqueue(DsaPracticeDbContext, SubmissionJudgeRequested) void
    }
    class OutboxWriter {
        -options : IOptions~RabbitMqOptions~
        -timeProvider : TimeProvider
    }
    class OutboxRelay {
        <<BackgroundService>>
        #ExecuteAsync(CancellationToken)
        -SafeWaitAsync(PeriodicTimer, CancellationToken)$
    }
    class OutboxProcessor {
        +ProcessPendingAsync(CancellationToken) int
        +PurgeProcessedAsync(CancellationToken) int
        -Backoff(OutboxMessage, Exception, OutboxOptions)
    }
    class JudgedResultConsumer {
        <<BackgroundService>>
        #ExecuteAsync(CancellationToken)
        -HandleAsync(IChannel, BasicDeliverEventArgs, CancellationToken)
        -ConnectWithRetryAsync(CancellationToken)
    }
    class JudgedResultRecorder {
        +RecordAsync(SubmissionJudged, CancellationToken) RecordOutcome
        -MapVerdict(JudgeVerdict)$ SubmissionVerdict
    }
    class JudgeRequestFactory {
        <<static>>
        +Create(Submission, Question)$ SubmissionJudgeRequested
    }
    class RecordOutcome {
        <<enumeration>>
        Recorded
        AlreadyRecorded
        UnknownSubmission
    }

    IOutboxWriter <|.. OutboxWriter
    OutboxRelay ..> OutboxProcessor : scope per pass
    OutboxProcessor ..> IMessagePublisher
    JudgedResultConsumer ..> JudgedResultRecorder : scope per message
    JudgedResultRecorder ..> RecordOutcome
    SubmissionsService ..> IOutboxWriter
    SubmissionsService ..> JudgeRequestFactory
```

`OutboxProcessor` is split from `OutboxRelay` so one pass can be driven directly — by a test, or
later by anything that wants to flush on demand. Both background services create a **scope per unit
of work** via `IServiceScopeFactory`: a `BackgroundService` has no ambient request scope to borrow a
`DbContext` from. That is the correct use of the pattern, as opposed to a singleton service pulling
scoped dependencies out of the container per method.

## DsaPractice.Api — auth and errors

```mermaid
classDiagram
    class ICurrentUserProvider {
        <<interface>>
        +GetOrCreateAsync(CancellationToken) User
    }
    class CurrentUserProvider {
        -Identify(ClaimsPrincipal?) tuple
    }
    class AuthOptions {
        +bool RequireAuthentication
        +string LocalIssuer
    }
    class ApiException {
        <<abstract>>
        +int StatusCode
        +string Title
        +object? ExtendedDetail
    }
    class NotFoundException
    class ConflictException
    class BadRequestException
    class GlobalExceptionHandler {
        <<IExceptionHandler>>
        +TryHandleAsync(HttpContext, Exception, CancellationToken)
    }
    class ErrorTitles {
        <<static>>
        +ErrorNameSpace = "api.error"
    }
    class ApiErrorTitlesHelper {
        <<static>>
        +ToApiErrorTitle(int)$ string
    }

    ICurrentUserProvider <|.. CurrentUserProvider
    CurrentUserProvider ..> AuthOptions
    ApiException <|-- NotFoundException
    ApiException <|-- ConflictException
    ApiException <|-- BadRequestException
    GlobalExceptionHandler ..> ApiException
    GlobalExceptionHandler ..> ApiErrorTitlesHelper
    ApiErrorTitlesHelper ..> ErrorTitles
```

The hierarchy is shaped by **HTTP semantics, not by entity** — `NotFoundException("Question '…' was
not found.")`, never `QuestionNotFoundException`. Title strings have exactly one source:
`ToApiErrorTitle(statusCode)`, called both by the exception constructors and by the
`CustomizeProblemDetails` callback that handles responses nothing threw for.

## DsaPractice.Judge — execution

```mermaid
classDiagram
    class ISandboxExecutor {
        <<interface>>
        +ExecuteAsync(SubmissionJudgeRequested, CancellationToken) ExecutionOutcome
    }
    class DockerSandboxExecutor {
        -docker : IDockerClient
        -options : IOptions~SandboxOptions~
        -RunTestCaseAsync(...) TestCaseOutcome
        -CreateContainerAsync(...) string
        -CompileAsync(...) CompilationResult
        -BuildCommand(...)$ string[]
        -EnsureImageAsync(string, CancellationToken)
        -RemoveContainerAsync(string)
        -RemoveVolumeAsync(string)
        -SecurityOptions(SandboxOptions)$ IList~string~
    }
    class FakeSandboxExecutor {
        <<default until a runner is configured>>
    }
    class ExecutionOutcome {
        <<record>>
        +IReadOnlyList~TestCaseOutcome~ TestCases
        +string? CompileOutput
        +bool CompilationFailed
    }
    class TestCaseOutcome {
        <<record>>
        +Guid TestCaseId
        +int Ordinal
        +TestCaseStatus Status
        +string? ActualOutput
        +string? ErrorMessage
        +long ExecutionTimeMs
    }
    class TestCaseStatus {
        <<enumeration>>
        Passed
        WrongAnswer
        TimeLimitExceeded
        MemoryLimitExceeded
        RuntimeError
    }
    class VerdictAggregator {
        <<static>>
        +Aggregate(Guid, ExecutionOutcome)$ SubmissionJudged
    }
    class OutputComparer {
        <<static>>
        +Matches(string?, string)$ bool
    }
    class SandboxOptions {
        +double CpuCores
        +int PidsLimit
        +int StartupGraceMs
        +int MaxOutputBytes
        +int WorkspaceSizeMb
        +int CompileMemoryMb
        +int CompileTimeoutMs
        +string? Runtime
        +string SeccompProfile
        +Dictionary~string,LanguageRunner~ Runners
    }
    class LanguageRunner {
        +string Image
        +string SourceFileName
        +string[] RunCommand
        +double TimeLimitMultiplier
        +string? CompileImage
        +string[]? CompileCommand
        +string ArtifactPath
        +bool IsCompiled
    }

    ISandboxExecutor <|.. DockerSandboxExecutor
    ISandboxExecutor <|.. FakeSandboxExecutor
    DockerSandboxExecutor ..> SandboxOptions
    DockerSandboxExecutor ..> OutputComparer
    DockerSandboxExecutor --> ExecutionOutcome
    ExecutionOutcome *-- TestCaseOutcome
    TestCaseOutcome ..> TestCaseStatus
    SandboxOptions *-- LanguageRunner
    VerdictAggregator ..> ExecutionOutcome
```

The verdict is derived by `VerdictAggregator` rather than decided by the executor, so the rule lives
in one testable place regardless of which executor ran — which is what makes `FakeSandboxExecutor`
and `StubSandboxExecutor` useful rather than a parallel implementation of the rules.

## DsaPractice.Judge — messaging

```mermaid
classDiagram
    class JudgeRequestConsumer {
        <<BackgroundService>>
        #ExecuteAsync(CancellationToken)
        -HandleAsync(IChannel, BasicDeliverEventArgs, CancellationToken)
        -PublishAsync(SubmissionJudged, CancellationToken)
        -ConnectWithRetryAsync(CancellationToken) IConnection?
    }
    class ProcessedSubmissions {
        +ctor(int capacity)
        +TryMarkProcessed(Guid) bool
    }
    class JudgeOptions {
        +ushort Prefetch
        +bool UseFakeExecutor
        +int ProcessedCacheSize
    }

    JudgeRequestConsumer ..> ISandboxExecutor
    JudgeRequestConsumer ..> VerdictAggregator
    JudgeRequestConsumer ..> ProcessedSubmissions
    JudgeRequestConsumer ..> IMessagePublisher
    JudgeRequestConsumer ..> JudgeOptions
```

`ConnectWithRetryAsync` is why the Judge waits for the broker instead of failing to start: it has
nothing to do without one, unlike the Api, which still serves questions perfectly well.

Note that `JudgeOptions` does **not** nest `SandboxOptions`, even though the configuration sections
are `Judge` and `Judge:Sandbox`. They are bound and validated independently in `Program.cs`, so the
consumer takes `IOptions<JudgeOptions>` and the executor takes `IOptions<SandboxOptions>` — neither
drags the other's settings along.

## DsaPractice.Messaging — shared transport

```mermaid
classDiagram
    class IRabbitMqConnection {
        <<interface>>
        +GetAsync(CancellationToken) IConnection
    }
    class RabbitMqConnection {
        <<singleton, lazy>>
    }
    class IMessagePublisher {
        <<interface>>
        +PublishAsync(routingKey, type, messageId, body, CancellationToken)
    }
    class RabbitMqPublisher {
        <<knows no message types>>
    }
    class RabbitMqTopology {
        <<static>>
        +DeclareAsync(IConnection, RabbitMqOptions, CancellationToken)$
    }
    class RabbitMqOptions {
        +string Uri
        +string Exchange
        +string DeadLetterExchange
        +string JudgeRequestedRoutingKey
        +string JudgeRequestedQueue
        +string JudgedRoutingKey
        +string JudgedQueue
        +string DeadLetterQueue
        +string DeadLetterRoutingKey
    }
    class RabbitMqClientName {
        <<record>>
        +string Value
    }

    IRabbitMqConnection <|.. RabbitMqConnection
    IMessagePublisher <|.. RabbitMqPublisher
    RabbitMqPublisher ..> IRabbitMqConnection
    RabbitMqPublisher ..> RabbitMqOptions
    RabbitMqTopology ..> RabbitMqOptions
    RabbitMqConnection ..> RabbitMqClientName
```

`RabbitMqPublisher` takes an already-serialized body and a routing key. It knows nothing about
message types on purpose — the outbox row carries the routing key, type and payload, so the relay
needs no per-type knowledge either.

**Known gap:** this assembly is publish-side only, so both consumers hand-roll the same ~67 lines of
connect-with-retry, channel setup and deserialize-or-dead-letter. A `RabbitMqConsumer<TMessage>`
base class is roadmap item 23; the ack decision deliberately stays with the handler, because it is
business policy rather than transport.

## DsaPractice.Contracts — the wire

```mermaid
classDiagram
    class SubmissionJudgeRequested {
        <<record>>
        +Guid SubmissionId
        +Guid QuestionId
        +string Language
        +string SourceCode
        +int TimeLimitMs
        +int MemoryLimitMb
        +IReadOnlyList~JudgeTestCase~ TestCases
    }
    class JudgeTestCase {
        <<record>>
        +Guid TestCaseId
        +int Ordinal
        +string Input
        +string ExpectedOutput
    }
    class SubmissionJudged {
        <<record>>
        +Guid SubmissionId
        +JudgeVerdict Verdict
        +IReadOnlyList~TestCaseResult~ TestCaseResults
        +string? CompileOutput
    }
    class TestCaseResult {
        <<record>>
        +Guid TestCaseId
        +int Ordinal
        +bool Passed
        +string? ActualOutput
        +string? ErrorMessage
        +long ExecutionTimeMs
    }
    class JudgeVerdict {
        <<enumeration>>
        Accepted
        WrongAnswer
        TimeLimitExceeded
        MemoryLimitExceeded
        RuntimeError
        CompilationError
        InternalError
    }
    class ContractJson {
        <<static>>
        +Options : JsonSerializerOptions
    }

    SubmissionJudgeRequested *-- JudgeTestCase
    SubmissionJudged *-- TestCaseResult
    SubmissionJudged ..> JudgeVerdict
```

`SubmissionJudgeRequested` carries **every** test case, hidden ones included. Hiding test cases is a
read-API rule, not a judging one — and the Judge cannot look them up, because it has no database
(D7).

`JudgeVerdict` mirrors the Api's `SubmissionVerdict` **by name rather than sharing the type**, and
`JudgedResultRecorder.MapVerdict` is an explicit `switch` rather than a name parse: the two enums are
allowed to drift, and a new value on either side should be a compile error, not a silent mismatch.

## DsaPractice.ContentSeeding

```mermaid
classDiagram
    class ContentLoader {
        <<static>>
        +Load(string contentRoot)$ IReadOnlyList~QuestionContent~
        -LoadQuestion(string, List~string~)$ QuestionContent?
        -LoadMetadata(...)$ QuestionMetadata?
        -LoadStatement(...)$ string?
        -LoadStarters(...)$ Dictionary~string,string~
        -LoadTestCases(...)$ List~TestCaseContent~
        -Normalise(string)$ string
        -NormaliseSource(string)$ string
    }
    class QuestionContent {
        <<record>>
        +string Slug
        +string Title
        +QuestionDifficulty Difficulty
        +IReadOnlyList~string~ Tags
        +int TimeLimitMs
        +int MemoryLimitMb
        +string Statement
        +IReadOnlyDictionary~string,string~ Starters
        +IReadOnlyList~TestCaseContent~ TestCases
    }
    class TestCaseContent {
        <<record>>
        +int Ordinal
        +string Input
        +string ExpectedOutput
        +bool IsHidden
    }
    class QuestionSeeder {
        +SeedAsync(IReadOnlyList~QuestionContent~, CancellationToken) SeedResult
        -ToEntity(QuestionContent)$ Question
        -Apply(QuestionContent, Question)$ void
        -StartersMatch(...)$ bool
        -ReconcileTestCases(QuestionContent, Question) int
        -CountChangedQuestions() int
    }
    class SeedResult {
        <<record>>
        +int Created
        +int Updated
        +int Unchanged
        +int TestCasesRemoved
    }
    class ContentException

    ContentLoader --> QuestionContent
    ContentLoader ..> ContentException
    QuestionContent *-- TestCaseContent
    QuestionSeeder ..> QuestionContent
    QuestionSeeder --> SeedResult
```

`CountChangedQuestions` reads EF's change tracker rather than comparing by hand, which is what makes
"unchanged content writes nothing" true rather than aspirational. `StartersMatch` exists for the same
reason: a `jsonb` map that round-trips with its keys in a different order is the same starter set,
and rewriting it would report every question as updated on every run.

## Frontend — component and data flow

```mermaid
graph TB
    app["App.tsx<br/><i>routes</i>"]:::client
    layout["Layout"]:::client
    qsp["QuestionsPage"]:::client
    qp["QuestionPage"]:::client
    nf["NotFoundPage"]:::client
    md["Markdown<br/><i>no rehype-raw</i>"]:::client
    badge["DifficultyBadge"]:::client
    sp["SubmitPanel<br/><i>starters, language, submit</i>"]:::client
    vp["VerdictPanel"]:::client
    monaco["@monaco-editor/react"]:::infra

    queries["api/queries.ts<br/>useQuestions · useQuestion<br/>useSubmission · useCreateSubmission"]:::client
    client["api/client.ts<br/><i>types derived from schema.d.ts</i>"]:::client
    schema["api/schema.d.ts<br/><i>generated from OpenAPI</i>"]:::infra

    app --> layout
    layout --> qsp
    layout --> qp
    layout --> nf
    qp --> md
    qp --> badge
    qp --> sp
    sp --> vp
    sp --> monaco
    qsp --> queries
    qp --> queries
    sp --> queries
    queries --> client
    client --> schema

    classDef client fill:#e0e7ff,stroke:#4338ca,stroke-width:2px,color:#1e1b4b
    classDef infra fill:#e2e8f0,stroke:#475569,stroke-width:2px,color:#0f172a
```

`schema.d.ts` is generated from the running Api's OpenAPI document and committed, so a contract
change breaks the **build** rather than the page. `Markdown` deliberately omits `rehype-raw`, so a
question statement cannot inject markup.
