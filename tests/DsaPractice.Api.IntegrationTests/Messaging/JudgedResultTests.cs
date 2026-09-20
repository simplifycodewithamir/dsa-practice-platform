using System.Net.Http.Json;
using System.Text.Json;
using DsaPractice.Api.Endpoints;
using DsaPractice.Api.IntegrationTests.Fixtures;
using DsaPractice.Api.Messaging;
using DsaPractice.Contracts;
using DsaPractice.DataAccess;
using DsaPractice.DataAccess.Entities;
using DsaPractice.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DsaPractice.Api.IntegrationTests.Messaging;

/// <summary>
/// The recorder is driven directly rather than through the broker: what matters here is what it
/// does to the database, and the consumer that wraps it is covered by the Judge's own tests plus
/// the end-to-end run.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class JudgedResultTests(ApiWebApplicationFactory factory)
{
    private const string OwnerSubject = "judged-result-tests-owner";

    [Fact]
    public async Task RecordAsync_AcceptedResult_CompletesTheSubmissionWithItsTestResults()
    {
        var (submissionId, testCaseIds) = await SeedSubmissionAsync();

        var outcome = await RecordAsync(new SubmissionJudged(
            submissionId,
            JudgeVerdict.Accepted,
            [
                new TestCaseResult(testCaseIds[0], 1, true, "one", null, 12),
                new TestCaseResult(testCaseIds[1], 2, true, "two", null, 15)
            ]));

        Assert.Equal(RecordOutcome.Recorded, outcome);
        var submission = await LoadAsync(submissionId);
        Assert.Equal(SubmissionStatus.Completed, submission.Status);
        Assert.Equal(SubmissionVerdict.Accepted, submission.Verdict);
        Assert.NotNull(submission.CompletedAtUtc);
        Assert.Equal([1, 2], submission.TestResults.OrderBy(r => r.Ordinal).Select(r => r.Ordinal));
    }

    [Fact]
    public async Task RecordAsync_FailingResult_StoresTheVerdictAndTheFailingOutput()
    {
        var (submissionId, testCaseIds) = await SeedSubmissionAsync();

        await RecordAsync(new SubmissionJudged(
            submissionId,
            JudgeVerdict.WrongAnswer,
            [
                new TestCaseResult(testCaseIds[0], 1, true, "one", null, 10),
                new TestCaseResult(testCaseIds[1], 2, false, "nope", null, 11)
            ]));

        var submission = await LoadAsync(submissionId);
        Assert.Equal(SubmissionVerdict.WrongAnswer, submission.Verdict);
        var failed = submission.TestResults.Single(r => r.Ordinal == 2);
        Assert.False(failed.Passed);
        Assert.Equal("nope", failed.ActualOutput);
    }

    [Fact]
    public async Task RecordAsync_SameResultTwice_LeavesTheFirstOneAlone()
    {
        var (submissionId, testCaseIds) = await SeedSubmissionAsync();
        await RecordAsync(new SubmissionJudged(submissionId, JudgeVerdict.Accepted,
            [new TestCaseResult(testCaseIds[0], 1, true, "one", null, 10)]));

        // At-least-once delivery makes this normal, not exceptional -- and a later result must not
        // overwrite a verdict the user has already been shown.
        var outcome = await RecordAsync(new SubmissionJudged(submissionId, JudgeVerdict.RuntimeError,
            [new TestCaseResult(testCaseIds[0], 1, false, null, "boom", 10)]));

        Assert.Equal(RecordOutcome.AlreadyRecorded, outcome);
        var submission = await LoadAsync(submissionId);
        Assert.Equal(SubmissionVerdict.Accepted, submission.Verdict);
        Assert.Single(submission.TestResults);
    }

    [Fact]
    public async Task RecordAsync_UnknownSubmission_IsDiscarded()
    {
        var outcome = await RecordAsync(new SubmissionJudged(Guid.NewGuid(), JudgeVerdict.Accepted, []));

        Assert.Equal(RecordOutcome.UnknownSubmission, outcome);
    }

    [Fact]
    public async Task RecordAsync_CompilationError_StoresCompileOutputAndNoTestResults()
    {
        var (submissionId, _) = await SeedSubmissionAsync();

        await RecordAsync(new SubmissionJudged(submissionId, JudgeVerdict.CompilationError, [], "error CS1002: ; expected"));

        var submission = await LoadAsync(submissionId);
        Assert.Equal(SubmissionVerdict.CompilationError, submission.Verdict);
        Assert.Equal("error CS1002: ; expected", submission.CompileOutput);
        Assert.Empty(submission.TestResults);
    }

    [Fact]
    public async Task RecordAsync_HugeOutput_IsTruncatedToTheStoredLimit()
    {
        var (submissionId, testCaseIds) = await SeedSubmissionAsync();
        var flood = new string('x', SubmissionTestResult.MaxOutputLength * 3);

        await RecordAsync(new SubmissionJudged(submissionId, JudgeVerdict.WrongAnswer,
            [new TestCaseResult(testCaseIds[0], 1, false, flood, null, 10)]));

        // Submitted code decides this string's length, so the database must not.
        var submission = await LoadAsync(submissionId);
        Assert.Equal(SubmissionTestResult.MaxOutputLength, submission.TestResults.Single().ActualOutput!.Length);
    }

    [Fact]
    public async Task GetSubmission_AfterJudging_ReturnsResultsButNeverHiddenOutput()
    {
        var (submissionId, testCaseIds) = await SeedSubmissionAsync();
        await RecordAsync(new SubmissionJudged(submissionId, JudgeVerdict.WrongAnswer,
            [
                new TestCaseResult(testCaseIds[0], 1, true, "visible-output", null, 10),
                new TestCaseResult(testCaseIds[1], 2, false, "hidden-output", "hidden-error", 11)
            ]));

        using var client = factory.CreateAuthenticatedClient(OwnerSubject);
        using var response = await client.GetAsync($"/api/v1/submissions/{submissionId}", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<SubmissionResponse>(TestJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(SubmissionVerdict.WrongAnswer, body!.Verdict);
        var sample = body.TestResults!.Single(r => r.Ordinal == 1);
        var hidden = body.TestResults!.Single(r => r.Ordinal == 2);
        Assert.Equal("visible-output", sample.ActualOutput);
        // The user learns the hidden case failed, never what it produced -- otherwise hidden tests
        // could be reconstructed one submission at a time.
        Assert.True(hidden.IsHidden);
        Assert.False(hidden.Passed);
        Assert.Null(hidden.ActualOutput);
        Assert.Null(hidden.ErrorMessage);
    }

    private async Task<RecordOutcome> RecordAsync(SubmissionJudged result)
    {
        using var scope = factory.Services.CreateScope();
        var recorder = scope.ServiceProvider.GetRequiredService<JudgedResultRecorder>();
        return await recorder.RecordAsync(result, TestContext.Current.CancellationToken);
    }

    private async Task<Submission> LoadAsync(Guid submissionId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();
        return await db.Submissions
            .AsNoTracking()
            .Include(s => s.TestResults)
            .SingleAsync(s => s.Id == submissionId, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A pending submission against a question with one sample and one hidden test case, owned by
    /// a user the caller can authenticate as (reads are owner-or-admin since item 19).
    /// </summary>
    private async Task<(Guid SubmissionId, List<Guid> TestCaseIds)> SeedSubmissionAsync()
    {
        var question = TestData.NewQuestion();
        question.TestCases =
        [
            TestData.NewTestCase(question.Id, 1, "1", "one"),
            TestData.NewTestCase(question.Id, 2, "2", "two", isHidden: true)
        ];
        await TestData.SeedAsync(factory, question);

        var owner = await TestData.SeedUserAsync(factory, OwnerSubject);
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            QuestionId = question.Id,
            OwnerUserId = owner.Id,
            Language = "python",
            SourceCode = "print(1)",
            SubmittedAtUtc = DateTimeOffset.UtcNow
        };

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();
        db.Submissions.Add(submission);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (submission.Id, [.. question.TestCases.OrderBy(tc => tc.Ordinal).Select(tc => tc.Id)]);
    }
}
