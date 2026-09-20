using DsaPractice.Api.Endpoints;
using DsaPractice.DataAccess.Entities;
using DsaPractice.DataAccess.Enums;
using Xunit;

namespace DsaPractice.Api.UnitTests.Endpoints;

public class SubmissionResponseTests
{
    [Fact]
    public void FromEntity_ReportsTheOwnerAsTheUser_NotAnythingTheCallerSent()
    {
        // Decision D3: who a submission belongs to is the stored owner, never a field on the request.
        var submission = NewSubmission();

        var response = SubmissionResponse.FromEntity(submission);

        Assert.Equal(submission.OwnerUserId, response.UserId);
    }

    [Fact]
    public void FromEntity_PendingSubmission_HasNoVerdictOrCompletionYet()
    {
        var response = SubmissionResponse.FromEntity(NewSubmission());

        Assert.Equal(SubmissionStatus.Pending, response.Status);
        Assert.Null(response.Verdict);
        Assert.Null(response.CompletedAtUtc);
        Assert.Null(response.CompileOutput);
    }

    [Fact]
    public void FromEntity_JudgedSubmission_CarriesTheVerdictAndCompletionTime()
    {
        var submission = NewSubmission();
        submission.Status = SubmissionStatus.Completed;
        submission.Verdict = SubmissionVerdict.WrongAnswer;
        submission.CompletedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(5);
        submission.CompileOutput = "warning CS0168";

        var response = SubmissionResponse.FromEntity(submission);

        Assert.Equal(SubmissionStatus.Completed, response.Status);
        Assert.Equal(SubmissionVerdict.WrongAnswer, response.Verdict);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(5), response.CompletedAtUtc);
        Assert.Equal("warning CS0168", response.CompileOutput);
    }

    [Fact]
    public void FromEntity_NeverReturnsTheSourceCodeOrTheStoredResultsDirectly()
    {
        // Per-test-case results are redacted by the service before they are attached, so the
        // straight mapping must start empty rather than exposing the stored rows as they are.
        var submission = NewSubmission();
        submission.TestResults =
        [
            new SubmissionTestResult
            {
                SubmissionId = submission.Id,
                TestCaseId = Guid.NewGuid(),
                Ordinal = 1,
                Passed = false,
                ActualOutput = "leaked hidden output"
            }
        ];

        var response = SubmissionResponse.FromEntity(submission);

        Assert.Empty(response.TestResults!);
    }

    private static Submission NewSubmission() => new()
    {
        Id = Guid.NewGuid(),
        QuestionId = Guid.NewGuid(),
        OwnerUserId = Guid.NewGuid(),
        Language = "python",
        SourceCode = "print(1)",
        SubmittedAtUtc = DateTimeOffset.UnixEpoch
    };
}
