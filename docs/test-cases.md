# Test cases

Hand-executable specifications. Each one can be run **manually** by following its steps, or read as
the spec a **Playwright** test is written from. Every case says which it is today.

For the strategy — what is tested at which layer and why — see [testing](testing.md).

- **Automated** — an automated test already asserts this. The "Automated by" column names it.
- **Manual** — no automated coverage. Run it by hand; these are the candidates worth automating next.

---

## Setting up

### Common preconditions

Referred to below as **ENV-READY**.

```bash
cd ~/my-repos/dsa-practice-platform
cp .env.example .env                                  # one-time
docker compose --profile full-stack up -d --build     # Api :8080, Judge, Postgres, RabbitMQ
docker compose ps                                     # postgres + rabbitmq healthy, migrator exited 0
cd frontend && npm run dev                            # SPA on :5173
```

Verify before starting — if either fails, stop and fix it rather than recording failures:

```bash
curl -s -o /dev/null -w "api %{http_code}\n"  http://localhost:8080/api/v1/questions   # 200
curl -s -o /dev/null -w "spa %{http_code}\n"  http://localhost:5173/                   # 200
```

> **The Judge must not be in fake mode.** `Judge:UseFakeExecutor` is `false` in `appsettings.json`,
> but if it is ever `true` every submission returns `Accepted` in milliseconds and every judging case
> below is meaningless. The Judge logs a warning at startup when it is active.

### Running the automated suite

```bash
dotnet test --solution source/DsaPractice.slnx     # 164
cd frontend && npm test                            # 36
cd frontend && npm run test:e2e                    # 6 — needs the full stack up
npx playwright test --ui                           # step through visually
```

### Test data

Three questions are seeded. Their I/O formats are what every case below depends on.

| Slug | Difficulty | Input | Output |
|---|---|---|---|
| `two-sum` | Easy | Line 1: `n target`. Line 2: `n` integers | Two 0-based indices, smaller first |
| `valid-parentheses` | Medium | One line: the string | `true` or `false`, lowercase |
| `maximum-subarray` | Medium | Line 1: `n`. Line 2: `n` integers | One integer |

<details>
<summary><b>Known-good Python solutions</b> — verified against every sample and hidden test</summary>

**two-sum**
```python
import sys
data = sys.stdin.read().split()
n, target = int(data[0]), int(data[1])
nums = [int(x) for x in data[2:2 + n]]
seen = {}
for i, x in enumerate(nums):
    if target - x in seen:
        print(seen[target - x], i)
        break
    seen.setdefault(x, i)
```

**valid-parentheses**
```python
import sys
s = sys.stdin.readline().strip()
pairs = {')': '(', ']': '[', '}': '{'}
stack = []
ok = True
for c in s:
    if c in '([{':
        stack.append(c)
    elif not stack or stack.pop() != pairs[c]:
        ok = False
        break
print('true' if ok and not stack else 'false')
```

**maximum-subarray**
```python
import sys
data = sys.stdin.read().split()
n = int(data[0])
nums = [int(x) for x in data[1:1 + n]]
best = cur = nums[0]
for x in nums[1:]:
    cur = max(x, cur + x)
    best = max(best, cur)
print(best)
```

</details>

<details>
<summary><b>Known-good C# solution</b> for two-sum — verified through the Judge's own compile step</summary>

```csharp
public class Program
{
    public static void Main()
    {
        string[] header = Console.ReadLine()!.Split(' ');
        int n = int.Parse(header[0]);
        int target = int.Parse(header[1]);
        int[] nums = Array.ConvertAll(Console.ReadLine()!.Split(' '), int.Parse);

        int[] answer = new Solution().TwoSum(nums, target);
        Console.WriteLine($"{answer[0]} {answer[1]}");
    }
}

public class Solution
{
    public int[] TwoSum(int[] nums, int target)
    {
        var seen = new Dictionary<int, int>();
        for (int i = 0; i < nums.Length; i++)
        {
            if (seen.TryGetValue(target - nums[i], out int j)) return [j, i];
            seen.TryAdd(nums[i], i);
        }
        return [];
    }
}
```

No `using` lines needed — the sandbox injects `global using` for `System`,
`System.Collections.Generic`, `System.IO`, `System.Linq`, `System.Text`, `System.Threading` and
`System.Threading.Tasks`.

</details>

**Deliberately bad submissions**, used throughout:

| Name | Language | Code | Produces |
|---|---|---|---|
| **WRONG** | Python | `print('0 0')` | Wrong answer |
| **CRASH** | Python | `raise ValueError('boom')` | Runtime error |
| **HANG** | Python | `while True: pass` | Time limit exceeded |
| **SLOW** | Python | nested loop over all pairs | Time limit exceeded on the large hidden test |
| **NOCOMPILE** | C# | `public class Program { public static void Main() { int x = "nope"; } }` | Compilation error |

---

## TC-BR · Browsing and discovery

### TC-BR-01 · The question list loads
**Priority** High · **Automated** — `QuestionsPage.test.tsx`, e2e *"a student can browse to a question and see it"*

1. Open `http://localhost:5173/`.

**Expected:** heading "Questions". All three seeded questions listed, each with a difficulty badge.
No error, no empty state.

### TC-BR-02 · A question links to its slug URL
**Priority** High · **Automated** — `QuestionsPage.test.tsx`

1. From the list, click **Two Sum**.

**Expected:** URL is `/problems/two-sum` — the slug, not an id. The question page renders.

### TC-BR-03 · Filter by difficulty
**Priority** Medium · **Automated** — `QuestionsPage.test.tsx`, e2e *"filtering the list by difficulty narrows it"*

1. On the list, set **Difficulty** to `Medium`.

**Expected:** Valid Parentheses and Maximum Subarray Sum remain; Two Sum is hidden. No request is
made — filtering is in the browser.

### TC-BR-04 · Filter by topic
**Priority** Medium · **Automated** — `QuestionsPage.test.tsx`

1. Open the topic filter.

**Expected:** only topics that exist on the seeded questions are offered (e.g. `array`,
`hash-table`, `dynamic-programming`), not a hardcoded list. Selecting one narrows the list.

### TC-BR-05 · A filter combination matching nothing says so
**Priority** Medium · **Automated** — `QuestionsPage.test.tsx`

1. Combine difficulty `Easy` with a topic no Easy question has.

**Expected:** an explicit "nothing matches" message. **Not** a blank area — a blank list reads as
"no questions exist".

### TC-BR-06 · A failed list request is reported as a failure
**Priority** High · **Automated** — `QuestionsPage.test.tsx`

1. Stop the Api: `docker compose stop api`.
2. Reload `http://localhost:5173/`.

**Expected:** an error message. **Not** an empty list. Restart with `docker compose start api`.

---

## TC-QP · The question page

### TC-QP-01 · Statement, limits and difficulty
**Priority** High · **Automated** — `QuestionPage.test.tsx`, e2e

1. Open `/problems/two-sum`.

**Expected:** title, difficulty badge, tags, and the question's own limits (`1000 ms`, `256 MB`) —
the same values the Judge enforces.

### TC-QP-02 · The statement renders as markdown
**Priority** High · **Automated** — `QuestionPage.test.tsx`, e2e

**Expected:** "Input" and "Output" are real `<h2>` headings, code spans are formatted. No raw `##`
or backticks visible.

### TC-QP-03 · Sample test cases are shown
**Priority** High · **Automated** — `QuestionPage.test.tsx`, e2e

**Expected:** an "Examples" section. For `two-sum`, Example 1 shows input `4 9 / 2 7 11 15` and
output `0 1`.

### TC-QP-04 · Hidden test cases are never shown
**Priority** **Critical** · **Automated** — `QuestionsEndpointsTests.GetQuestionBySlug_ExistingSlug_ReturnsDetailWithSampleTestCasesOnlyInOrder`

1. `curl -s http://localhost:8080/api/v1/questions/two-sum | python3 -m json.tool`

**Expected:** `sampleTestCases` contains **only** the samples. No hidden input or output appears
anywhere in the response. Cross-check the count against `ls content/questions/two-sum/tests/hidden/`.

### TC-QP-05 · An unknown slug is explained
**Priority** Medium · **Automated** — `QuestionPage.test.tsx`, `QuestionsEndpointsTests.GetQuestionBySlug_UnknownSlug_Returns404WithNotFoundTitle`

1. Open `/problems/no-such-question`.

**Expected:** "No such question" in its own words, and a link back. Not a generic failure.

### TC-QP-06 · A malformed slug 404s before any query
**Priority** Medium · **Automated** — `QuestionsEndpointsTests.GetQuestionBySlug_MalformedSlug_Returns404WithNotFoundTitle`

1. `curl -i http://localhost:8080/api/v1/questions/Not__A__Slug`

**Expected:** `404` with a `ProblemDetails` body whose `title` is `api.error.notfound`. The route
constraint rejects it, so no handler runs — but the body must still match the thrown-404 shape.

### TC-QP-07 · Markdown cannot inject markup
**Priority** High · **Manual**

1. Add `<img src=x onerror="alert(1)">` to a question's `statement.md`.
2. `docker compose run --rm migrator`, reload the page.

**Expected:** rendered as **text**. No alert, no element in the DOM. (`react-markdown` without
`rehype-raw`.) Revert the content afterwards.

---

## TC-ED · Editor and starter code

### TC-ED-01 · The editor opens with the question's starter
**Priority** High · **Automated** — `SubmitPanel.test.tsx`, e2e *"the editor opens with the question's starter"*

1. Open `/problems/two-sum`.

**Expected:** the editor is **not** empty. It contains the authored Python skeleton with `two_sum`
and `# Your code here.`, parsing already written.

### TC-ED-02 · Switching language swaps an untouched starter
**Priority** High · **Automated** — `SubmitPanel.test.tsx`, e2e

1. Without typing anything, change **Language** to `C#`.

**Expected:** the C# skeleton with `TwoSum` replaces it.

### TC-ED-03 · Switching language never eats typed code
**Priority** **Critical** · **Automated** — `SubmitPanel.test.tsx`

1. Replace the editor contents with `my own work`.
2. Change language.

**Expected:** `my own work` is still there, untouched.

### TC-ED-04 · Untouched means *any* language's starter
**Priority** Medium · **Automated** — `SubmitPanel.test.tsx`

1. Switch Python → C# → Python without typing.

**Expected:** the Python starter comes back. Not the C# one left in place.

### TC-ED-05 · Fallback for a language with no starter
**Priority** Medium · **Automated** — `SubmitPanel.test.tsx`

1. Remove `content/questions/two-sum/starters/csharp.cs`, re-run the migrator, reload.
2. Switch to C#.

**Expected:** `// Read from stdin, print the answer.` Not an empty editor, not an error. Restore
the file and re-run the migrator.

### TC-ED-06 · Navigating between questions resets the editor
**Priority** Medium · **Manual**

1. On `/problems/two-sum`, submit anything and wait for a verdict.
2. Navigate to `/problems/valid-parentheses`.

**Expected:** the editor shows *that* question's starter, and the previous verdict is gone.

### TC-ED-07 · Every starter compiles
**Priority** High · **Automated** — `tools/check-starters.sh`

1. `tools/check-starters.sh`

**Expected:** `ok` for all six. A starter that does not compile shows a solver an error they did not
cause.

### TC-ED-08 · Submit is disabled on an empty editor
**Priority** Medium · **Automated** — `SubmitPanel.test.tsx`

1. Select all in the editor and delete.

**Expected:** **Submit** is disabled.

---

## TC-SU · Submitting and verdicts

Every case here: **ENV-READY**, on `/problems/two-sum`, replacing the editor contents.

### TC-SU-01 · A correct Python solution is Accepted
**Priority** **Critical** · **Automated** — e2e *"a correct solution is accepted"*, `PythonRunnerTests.ReferenceSolution_AgainstTheRealTestCases_IsAccepted`

1. Paste the known-good Python two-sum solution. Submit.

**Expected:** button reads "Judging…" and is disabled. Within ~60 s the verdict is **Accepted**.
Every test case is listed; hidden ones show pass/fail and a duration but no output.

### TC-SU-02 · A correct C# solution is Accepted
**Priority** **Critical** · **Automated** — `CSharpRunnerTests.ReferenceSolution_IsAcceptedAcrossEveryTestCase`

1. Switch to C#, paste the known-good C# solution. Submit.

**Expected:** **Accepted**. Slower than Python's first run — the compile step runs once per
submission and the SDK image may need pulling.

### TC-SU-03 · A wrong answer shows the student their own output
**Priority** High · **Automated** — e2e *"a wrong solution is rejected"*, `DockerSandboxExecutorTests.Execute_WrongOutput_IsWrongAnswerAndKeepsWhatWasPrinted`

1. Submit **WRONG**.

**Expected:** **Wrong answer**. The failing *sample* case shows "Your output" as `0 0` alongside what
was expected.

### TC-SU-04 · A crash is a runtime error, not a wrong answer
**Priority** High · **Automated** — e2e, `PythonRunnerTests.Exception_IsRuntimeErrorWithTheTraceback`

1. Submit **CRASH**.

**Expected:** **Runtime error**, with Python's traceback mentioning `ValueError: boom`. Not "Wrong
answer".

### TC-SU-05 · An infinite loop is a time limit, and the container is gone
**Priority** High · **Automated** — `PythonRunnerTests.InfiniteLoop_IsTimeLimitExceeded`, `DockerSandboxExecutorTests.Execute_InfiniteLoop_IsTimeLimitExceededAndTheContainerIsGone`

1. Submit **HANG**.
2. While it runs: `watch -n1 'docker ps --filter ancestor=python:3.12-alpine'`

**Expected:** **Time limit exceeded** within roughly *limit × 2 + 3 s grace*. Afterwards **no**
sandbox container remains.

### TC-SU-06 · A brute-force solution exceeds the limit on the large test
**Priority** Medium · **Automated** — `PythonRunnerTests.BruteForceSolution_OnTheLargeTestCase_ExceedsTheTimeLimit`

1. Submit **SLOW** (nested loop over all pairs).

**Expected:** passes the small samples, then **Time limit exceeded** on the large hidden case. This
is what makes the question's "use a hash map" note meaningful.

### TC-SU-07 · Code that does not compile never runs
**Priority** High · **Automated** — `CSharpRunnerTests.CodeThatDoesNotCompile_IsCompilationErrorAndNothingRuns`

1. Switch to C#, submit **NOCOMPILE**.

**Expected:** **Compilation error**, with the compiler's message (`CS0029: Cannot implicitly convert
type 'string' to 'int'`). **No per-test-case results at all** — nothing ran.

### TC-SU-08 · Judging stops at the first failure
**Priority** Medium · **Automated** — `DockerSandboxExecutorTests.Execute_StopsAtTheFirstFailingTestCase`

1. Submit a solution that fails test 1.

**Expected:** results stop at the failing case. Later cases are not run — the verdict is already
decided.

### TC-SU-09 · A second submission cannot clobber an unread result
**Priority** Medium · **Automated** — `SubmitPanel.test.tsx`

1. Submit, then immediately try to submit again.

**Expected:** **Submit** is disabled and reads "Judging…" until the verdict arrives.

### TC-SU-10 · Polling stops once judged
**Priority** Medium · **Automated** — `SubmitPanel.test.tsx`

1. Submit and wait for the verdict.
2. Open DevTools → Network and watch for 30 s.

**Expected:** requests to `/api/v1/submissions/{id}` **stop**. An idle page makes no requests.

### TC-SU-11 · An unsupported language is rejected at the edge
**Priority** Medium · **Automated** — `SubmissionsEndpointsTests.CreateSubmission_UnsupportedLanguage_Returns400`

```bash
curl -i -X POST http://localhost:8080/api/v1/submissions \
  -H 'content-type: application/json' \
  -d '{"questionId":"<id>","language":"rust","sourceCode":"fn main(){}"}'
```

**Expected:** `400` with `title: api.error.badrequest` and an `extendedDetail` naming the field and
the allowed languages. Nothing is queued.

### TC-SU-12 · An unknown question is rejected
**Priority** Medium · **Automated** — `SubmissionsEndpointsTests.CreateSubmission_UnknownQuestionId_Returns404`, `JudgeRequestPublishingTests.CreateSubmission_UnknownQuestion_PublishesNothing`

1. POST a submission with a random `questionId`.

**Expected:** `404`. **And nothing is published** — check the outbox is empty for it.

---

## TC-VD · What a verdict shows

### TC-VD-01 · Verdicts are spelled out for a person
**Priority** Medium · **Automated** — `VerdictPanel.test.tsx`

**Expected:** "Time limit exceeded", never `TimeLimitExceeded`.

### TC-VD-02 · Hidden test cases never show output
**Priority** **Critical** · **Automated** — `VerdictPanel.test.tsx`, `JudgedResultTests.GetSubmission_AfterJudging_ReturnsResultsButNeverHiddenOutput`

1. Submit **WRONG**, wait for the verdict.
2. Inspect the raw response: `curl -s http://localhost:8080/api/v1/submissions/{id} | python3 -m json.tool`

**Expected:** hidden results carry `isHidden: true`, `passed`, `executionTimeMs` — and
`actualOutput: null`, `errorMessage: null`. The Api withholds it; the UI would not render it even if
it arrived. **If output leaks here, the hidden tests can be reconstructed one submission at a time.**

### TC-VD-03 · A judge failure is not blamed on the submitter
**Priority** Medium · **Automated** — `VerdictPanel.test.tsx`, `JudgeRequestConsumerTests.JudgeRequest_ExecutorThrows_ReportsInternalErrorAndDeadLettersTheRequest`

**Expected:** an `InternalError` verdict says explicitly that it is not the submitter's fault.

### TC-VD-04 · Per-test timing is shown
**Priority** Low · **Automated** — `VerdictPanel.test.tsx`

**Expected:** each case shows a duration. It is the container's own measured time, not wall clock
around the call — so it does not include container startup.

---

## TC-RS · Resilience

### TC-RS-01 · A broker outage does not fail a submission
**Priority** **Critical** · **Automated** — `OutboxTests.CreateSubmission_WritesOutboxRowAndPublishesNothingInline`, `ProcessPendingAsync_UnroutableMessage_LeavesRowPendingWithBackoffAndError`

1. `docker compose stop rabbitmq`
2. Submit a correct solution.
3. Watch the row:

```bash
docker compose exec postgres psql -U dsapractice -d dsapractice -c \
 'SELECT "MessageId","AttemptCount","NextAttemptAtUtc","ProcessedAtUtc" FROM "OutboxMessages" ORDER BY "OccurredAtUtc" DESC LIMIT 3;'
```
4. `docker compose start rabbitmq`

**Expected:** the submission returns **201**, not 500. The SPA shows "Judging…". `AttemptCount`
climbs and `NextAttemptAtUtc` backs off (2 s, 4 s, 8 s…). Once the broker is back the relay publishes
by itself and the verdict arrives with no user action.

### TC-RS-02 · The dead-letter queue is empty in normal operation
**Priority** High · **Manual**

1. Open `http://localhost:15672` (credentials in `.env`) → Queues.

**Expected:** `submission.dead-letter` has **0** messages. Anything there was rejected outright and
is worth investigating.

### TC-RS-03 · A duplicate result does not overwrite the first
**Priority** High · **Automated** — `JudgedResultTests.RecordAsync_SameResultTwice_LeavesTheFirstOneAlone`, `JudgeRequestConsumerTests.JudgeRequest_DeliveredTwice_IsOnlyExecutedOnce`

Manual approximation: republish a `submission.judged` message for an already-completed submission via
the management UI.

**Expected:** the submission is unchanged. No duplicate test-result rows.

### TC-RS-04 · The Judge waits for the broker rather than failing to start
**Priority** Medium · **Manual**

1. `docker compose stop rabbitmq`, then restart the Judge.

**Expected:** it logs "Cannot reach RabbitMQ; retrying in …" with a growing delay, and connects by
itself when the broker returns. It does **not** exit.

### TC-RS-05 · A malformed message is parked, not retried forever
**Priority** Medium · **Automated** — `JudgeRequestConsumerTests.JudgeRequest_MalformedPayload_IsDeadLetteredWithoutJudging`

1. Publish `not json` to `submission.judge-requested` via the management UI.

**Expected:** it appears in `submission.dead-letter`. The Judge logs it and carries on. No infinite
redelivery loop.

---

## TC-SB · Sandbox isolation

All automated in `SandboxEscapeTests` / `DockerSandboxExecutorTests`. **Run them rather than doing
this by hand** — submitting these through the UI is slower and proves the same thing.

```bash
dotnet test --project tests/DsaPractice.Judge.IntegrationTests/DsaPractice.Judge.IntegrationTests.csproj
```

| Case | A submission cannot… | Automated by |
|---|---|---|
| TC-SB-01 | reach the network | `Execute_NetworkAccess_Fails`, `Submission_CannotReachTheNetwork` |
| TC-SB-02 | write outside `/work` | `Execute_WritingOutsideTheWorkspace_Fails` |
| TC-SB-03 | run as root | `Execute_RunsAsNonRoot` |
| TC-SB-04 | exhaust the host with a fork bomb | `Execute_ForkBomb_IsContainedAndTheHostSurvives` |
| TC-SB-05 | flood output unboundedly | `Execute_OutputFlood_IsCappedNotObeyed` |
| TC-SB-06 | see the Docker socket | `Submission_CannotSeeTheDockerSocket` |
| TC-SB-07 | hold any capability | `Submission_HasNoCapabilities` |
| TC-SB-08 | regain privileges | `Submission_CannotRegainPrivileges` |
| TC-SB-09 | see other processes | `Submission_CannotSeeOtherProcesses` |
| TC-SB-10 | mount anything | `Submission_CannotMountAnything` |
| TC-SB-11 | write to kernel interfaces | `Submission_CannotWriteToKernelInterfaces` |
| TC-SB-12 | read host devices | `Submission_CannotReadHostDevices` |
| TC-SB-13 | raise its own rlimits past the pids cap | `Submission_RaisingItsOwnRlimits_StillCannotExceedThePidsCap` |
| TC-SB-14 | outlive the run | `Submission_CannotKeepAProcessAliveAfterTheRun` |
| TC-SB-15 | overwrite its own compiled artifacts | `CompiledProgram_CannotWriteToItsOwnArtifacts` |

**Run this suite after any change to `DockerSandboxExecutor`.** A control that is described but not
asserted is a control that will quietly stop working.

### TC-SB-16 · No sandbox containers or volumes leak
**Priority** High · **Manual** (partly automated by `CSharpRunnerTests.ArtifactVolume_IsRemovedAfterTheSubmission`)

1. `docker ps -a | wc -l` and `docker volume ls | wc -l` before.
2. Submit five solutions across both languages.
3. Compare after.

**Expected:** identical counts. Every sandbox container is removed in a `finally`, and the artifact
volume with it.

---

## TC-AU · Identity and ownership

Identity comes from the token, and since item 20 a submission requires one.

### TC-AU-01 · A user is provisioned on first sight
**Priority** High · **Automated** — `UserProvisioningTests.Submitting_WithAToken_ProvisionsTheUserOnFirstSight`

**Expected:** a `Users` row appears keyed by `(iss, sub)`. No registration step.

### TC-AU-02 · The same person reuses one row
**Priority** High · **Automated** — `UserProvisioningTests.Submitting_TwiceAsTheSamePerson_ReusesTheSameUser`

### TC-AU-03 · A caller cannot choose who a submission belongs to
**Priority** **Critical** · **Automated** — `UserProvisioningTests.Submitting_CannotChooseWhoTheSubmissionBelongsTo`

1. POST a submission with an extra `"userId"` field.

**Expected:** ignored entirely. Ownership comes from the caller's identity.

### TC-AU-04 · Someone else's submission is 404, not 403
**Priority** **Critical** · **Automated** — `UserProvisioningTests.ReadingSomeoneElsesSubmission_Returns404NotForbidden`

**Expected:** `404`. A `403` would confirm the id exists.

### TC-AU-05 · An admin can read anyone's
**Priority** Medium · **Automated** — `UserProvisioningTests.AnAdminCanReadAnyonesSubmission`

Role comes from the `Users` table, never a token claim.

### TC-AU-06 · An unreadable token is rejected, not treated as anonymous
**Priority** High · **Automated** — `UserProvisioningTests.AnUnreadableToken_Returns401`

**Expected:** `401`. Junk is not an identity, and since item 20 it is not anonymous access either.

### TC-AU-07 · Submitting without a token is refused, in ProblemDetails
**Priority** **Critical** · **Automated** — `UserProvisioningTests.Submitting_WithoutAToken_Returns401`,
and end-to-end against the running stack in `solve-a-question.spec.ts`

**Expected:** `401` with `"title": "api.error.unauthorized"` — the same body shape as every other
failure, not an empty response.

### TC-AU-08 · Reading a question needs no token
**Priority** **Critical** · **Automated** — `UserProvisioningTests.ReadingAQuestion_NeedsNoToken`

The public, indexable half of the product (D10). Requiring a login to read a statement would cost
the organic traffic the site runs on.

### TC-AU-09 · With enforcement off, a submission still has a real owner
**Priority** Medium · **Automated** — `UserProvisioningTests.WithEnforcementOff_SubmittingWithoutAToken_IsAttributedToTheLocalUser`

The escape hatch behind `Auth:RequireAuthentication`: the submission belongs to a
`local-development/anonymous` row, so the foreign key still holds.

### TC-AU-10 · A broken bearer configuration fails startup
**Priority** High · **Automated** — `AuthConfigurationValidatorTests`

Enforcement on with no issuer, with no audience, or with a symmetric development key in
Production, each refuse to start rather than silently change who gets in.

### TC-AU-11 · Signing in from the browser
**Priority** High · **Manual** — needs a real identity provider tenant (`docs/auth.md`)

1. Open a question signed out. 2. The button reads **Sign in to submit**. 3. Click it, sign in with
Google. 4. The browser returns to the same question.

**Expected:** the header shows the display name, the button reads **Submit**, and submitting
succeeds. Component-level coverage of the same behaviour, without a provider, is in
`SignInControl.test.tsx` and `SubmitPanel.test.tsx`.

---

## TC-CT · Content authoring

### TC-CT-01 · Re-running the migrator changes nothing
**Priority** High · **Automated** — `QuestionSeederTests.SeedAsync_RunTwiceWithSameContent_ChangesNothingTheSecondTime`

1. `docker compose run --rm migrator` twice.

**Expected:** the second run reports `0 created, 0 updated, 3 unchanged`.

### TC-CT-02 · Editing a statement updates in place
**Priority** High · **Automated** — `QuestionSeederTests.SeedAsync_EditedStatement_UpdatesInPlaceKeepingTheQuestionId`

**Expected:** `1 updated`, and the question id is unchanged — existing submissions still point at it.

### TC-CT-03 · Broken content reports every problem at once
**Priority** High · **Automated** — `ContentLoaderTests.Load_ReportsEveryProblemAtOnce`

1. Break two questions differently (delete one `statement.md`; delete one `.out`).
2. Run the migrator.

**Expected:** a non-zero exit listing **both** problems. **Nothing is written** — fixing one problem
only to fail on the next is a slow way to find out content is wrong.

### TC-CT-04 · A question removed from content is not deleted
**Priority** Medium · **Automated** — `QuestionSeederTests.SeedAsync_QuestionInDatabaseButNotInContent_IsLeftAlone`

**Expected:** still served by the Api. Deleting is a deliberate SQL act.

### TC-CT-05 · A starter is picked up by filename stem
**Priority** Medium · **Automated** — `ContentLoaderTests.Load_StartersFolder_KeysByFileStemAndIgnoresTheExtension`

1. Add `content/questions/two-sum/starters/python.txt` alongside `python.py`.
2. Run the migrator.

**Expected:** an error naming the duplicate stem — the extension is ignored, so stems must be
unique. Remove the file.

---

## TC-UX · Usability

All **manual**. Automating these is roadmap item 25a.

### TC-UX-01 · Usable at phone width
**Priority** Medium
1. DevTools → 375 × 667.
**Expected:** no horizontal page scroll. The editor is usable. Statement and editor do not overlap.

### TC-UX-02 · Keyboard only
**Priority** Medium
1. From the list, reach a question, the language select, the editor and Submit using `Tab` and `Enter` only.
**Expected:** every control reachable, with a **visible** focus indicator on each.

### TC-UX-03 · Labels and roles
**Priority** Medium
1. Inspect the language select and the editor.
**Expected:** real `<label>` elements. Errors are `role="alert"`, the verdict is `role="status"`.

### TC-UX-04 · Slow network
**Priority** Low
1. DevTools → Network → Slow 3G. Reload a question page.
**Expected:** a loading state, then content. No flash of an error, no layout jump.

### TC-UX-05 · Browser back after submitting
**Priority** Low
1. Submit, wait for the verdict, click a different question, press Back.
**Expected:** the question page renders correctly. No crash, no stale verdict from the other question.

---

## Regression checklist

Before a release, or after touching the judging path. Twenty minutes by hand.

| Order | Case | Why it is on the list |
|---|---|---|
| 1 | TC-SU-01 | The product, in one test |
| 2 | TC-SU-02 | The other language, and the compile path |
| 3 | TC-QP-04 + TC-VD-02 | Hidden data staying hidden — the leak that cannot be undone |
| 4 | TC-SU-03, 04, 05, 07 | All four failure verdicts distinguishable |
| 5 | TC-RS-01 | Durability under a broker outage |
| 6 | TC-SB-* (run the suite) | Isolation still holds |
| 7 | TC-ED-01 + TC-ED-07 | Starters arrive and compile |
| 8 | TC-CT-01 | The migrator is still idempotent |
| 9 | TC-RS-02 | Dead-letter queue empty |

Or, faster and stricter:

```bash
dotnet test --solution source/DsaPractice.slnx     # 164
cd frontend && npm test && npm run test:e2e        # 36 + 6
tools/check-starters.sh
```

---

## Writing a Playwright test from a case

The cases above are written in the order a Playwright test needs: precondition, steps, expected.
TC-ED-01 became this, near enough verbatim:

```ts
test("the editor opens with the question's starter, not an empty page", async ({ page }) => {
  await page.goto('/problems/two-sum');

  const editor = page.locator('.monaco-editor .view-lines');
  await expect(editor).toContainText('two_sum');
  await expect(editor).toContainText('Your code here');

  await page.getByLabel('Language').selectOption('csharp');
  await expect(editor).toContainText('TwoSum');
});
```

Three things this suite has learned the hard way:

- **Type into Monaco through the clipboard**, never keystrokes — it auto-indents and auto-closes
  brackets, which mangles Python. `writeSolution()` in `e2e/solve-a-question.spec.ts` does it
  correctly; reuse it.
- **Give judging room.** The suite runs with a 90 s timeout and a 30 s expect timeout, because a
  verdict is a queue round trip plus a container per test case. Do not shorten these.
- **Assert on roles, not classes.** `getByRole('status')` for the verdict, `getByRole('alert')` for
  errors — they survive restyling, and they double as an accessibility check.

Run a new test with `npx playwright test --ui` first; the time-travel view shows exactly what the
page looked like at each step.

## See also

- [Testing](testing.md) — the strategy these cases sit inside
- [Sequence diagrams](design/04-sequence-diagrams.md) — the flows the resilience cases exercise
- [Sandbox hardening](sandbox-hardening.md) — what each TC-SB case is defending
