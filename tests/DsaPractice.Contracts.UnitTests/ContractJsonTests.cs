using System.Text.Json;
using DsaPractice.Contracts;
using Xunit;

namespace DsaPractice.Contracts.UnitTests;

/// <summary>
/// The wire format between the Api and the Judge. These two services are deployed separately and
/// a queue can hold messages written by the version before the one reading them, so the shape is a
/// contract rather than an implementation detail: what is asserted here is what a message actually
/// looks like on the queue, not just that it round-trips in one process.
/// </summary>
public class ContractJsonTests
{
    [Fact]
    public void Serialize_Verdict_IsAStringNotAnOrdinal()
    {
        // With the default options an enum goes on the wire as a number, and inserting a member
        // would silently change what every already-queued message means.
        var json = JsonSerializer.Serialize(NewJudged(JudgeVerdict.WrongAnswer), ContractJson.Options);

        Assert.Contains("\"verdict\":\"WrongAnswer\"", json);
        Assert.DoesNotContain("\"verdict\":1", json);
    }

    [Fact]
    public void Serialize_PropertyNames_AreCamelCase()
    {
        var json = JsonSerializer.Serialize(NewRequest(), ContractJson.Options);

        Assert.Contains("\"submissionId\":", json);
        Assert.Contains("\"timeLimitMs\":", json);
        Assert.DoesNotContain("\"SubmissionId\":", json);
    }

    [Fact]
    public void Deserialize_JudgeRequest_RoundTripsEveryFieldTheJudgeNeeds()
    {
        var request = NewRequest();

        var roundTripped = RoundTrip(request);

        Assert.Equal(request.SubmissionId, roundTripped.SubmissionId);
        Assert.Equal(request.QuestionId, roundTripped.QuestionId);
        Assert.Equal(request.Language, roundTripped.Language);
        Assert.Equal(request.SourceCode, roundTripped.SourceCode);
        Assert.Equal(request.TimeLimitMs, roundTripped.TimeLimitMs);
        Assert.Equal(request.MemoryLimitMb, roundTripped.MemoryLimitMb);
        Assert.Equal(request.TestCases.Count, roundTripped.TestCases.Count);
    }

    [Fact]
    public void Deserialize_TestCases_KeepTheirIdsOrdinalsAndOrder()
    {
        var request = NewRequest();

        var roundTripped = RoundTrip(request);

        Assert.Equal(request.TestCases.Select(tc => tc.TestCaseId), roundTripped.TestCases.Select(tc => tc.TestCaseId));
        Assert.Equal(request.TestCases.Select(tc => tc.Ordinal), roundTripped.TestCases.Select(tc => tc.Ordinal));
    }

    [Fact]
    public void RoundTrip_SourceCode_SurvivesNewlinesQuotesAndNonAscii()
    {
        // Submitted code is arbitrary text, and it is the one field nobody sanitises.
        const string sourceCode = "print(\"héllo\\n\\t'quoted'\")\r\n# ← comment\n";
        var request = NewRequest() with { SourceCode = sourceCode };

        Assert.Equal(sourceCode, RoundTrip(request).SourceCode);
    }

    [Fact]
    public void RoundTrip_TestCaseInputAndExpectedOutput_ArePreservedByteForByte()
    {
        // Whitespace in these is the difference between a right answer and a wrong one.
        var request = NewRequest() with
        {
            TestCases = [new JudgeTestCase(Guid.NewGuid(), 1, "3\n1 2 3", "  6 \n")]
        };

        var roundTripped = RoundTrip(request);

        Assert.Equal("3\n1 2 3", roundTripped.TestCases[0].Input);
        Assert.Equal("  6 \n", roundTripped.TestCases[0].ExpectedOutput);
    }

    [Fact]
    public void RoundTrip_JudgedResult_KeepsTheVerdictAndEveryTestCaseResult()
    {
        var judged = NewJudged(JudgeVerdict.TimeLimitExceeded);

        var json = JsonSerializer.Serialize(judged, ContractJson.Options);
        var roundTripped = JsonSerializer.Deserialize<SubmissionJudged>(json, ContractJson.Options)!;

        Assert.Equal(judged.SubmissionId, roundTripped.SubmissionId);
        Assert.Equal(JudgeVerdict.TimeLimitExceeded, roundTripped.Verdict);
        var result = Assert.Single(roundTripped.TestCaseResults);
        Assert.Equal(judged.TestCaseResults[0].TestCaseId, result.TestCaseId);
        Assert.False(result.Passed);
        Assert.Equal(1234, result.ExecutionTimeMs);
    }

    [Fact]
    public void Deserialize_JudgedResultWithoutCompileOutput_LeavesItNull()
    {
        var json = """
            {"submissionId":"11111111-1111-1111-1111-111111111111","verdict":"Accepted","testCaseResults":[]}
            """;

        var judged = JsonSerializer.Deserialize<SubmissionJudged>(json, ContractJson.Options)!;

        Assert.Null(judged.CompileOutput);
        Assert.Empty(judged.TestCaseResults);
    }

    [Fact]
    public void Deserialize_MessageWithAnUnknownProperty_IsStillReadable()
    {
        // A newer publisher may add a field; an older consumer must keep working rather than
        // dead-lettering every message until it is redeployed.
        var json = """
            {"submissionId":"11111111-1111-1111-1111-111111111111","verdict":"Accepted","testCaseResults":[],"somethingNew":42}
            """;

        var judged = JsonSerializer.Deserialize<SubmissionJudged>(json, ContractJson.Options)!;

        Assert.Equal(JudgeVerdict.Accepted, judged.Verdict);
    }

    [Fact]
    public void Deserialize_PropertyNames_AreMatchedCaseInsensitively()
    {
        // JsonSerializerDefaults.Web, so a publisher that serialised with PascalCase is still read.
        var json = """
            {"SubmissionId":"11111111-1111-1111-1111-111111111111","Verdict":"Accepted","TestCaseResults":[]}
            """;

        var judged = JsonSerializer.Deserialize<SubmissionJudged>(json, ContractJson.Options)!;

        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), judged.SubmissionId);
        Assert.Equal(JudgeVerdict.Accepted, judged.Verdict);
    }

    [Fact]
    public void Deserialize_VerdictThisVersionDoesNotKnow_Throws()
    {
        // Better to reject the message than to record it as whatever the first enum member is.
        var json = """
            {"submissionId":"11111111-1111-1111-1111-111111111111","verdict":"SomethingNewer","testCaseResults":[]}
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SubmissionJudged>(json, ContractJson.Options));
    }

    [Theory]
    [InlineData(JudgeVerdict.Accepted)]
    [InlineData(JudgeVerdict.WrongAnswer)]
    [InlineData(JudgeVerdict.TimeLimitExceeded)]
    [InlineData(JudgeVerdict.MemoryLimitExceeded)]
    [InlineData(JudgeVerdict.RuntimeError)]
    [InlineData(JudgeVerdict.CompilationError)]
    [InlineData(JudgeVerdict.InternalError)]
    public void RoundTrip_EveryVerdict_SurvivesAsItsOwnName(JudgeVerdict verdict)
    {
        var json = JsonSerializer.Serialize(NewJudged(verdict), ContractJson.Options);

        Assert.Equal(verdict, JsonSerializer.Deserialize<SubmissionJudged>(json, ContractJson.Options)!.Verdict);
    }

    [Fact]
    public void Options_AreShared_SoBothSidesCannotConfigureThemDifferently()
    {
        // Publisher and consumer read the same static instance; a per-call copy would be how the
        // two ends drift apart.
        Assert.Same(ContractJson.Options, ContractJson.Options);
    }

    private static SubmissionJudgeRequested RoundTrip(SubmissionJudgeRequested request) =>
        JsonSerializer.Deserialize<SubmissionJudgeRequested>(
            JsonSerializer.Serialize(request, ContractJson.Options), ContractJson.Options)!;

    private static SubmissionJudgeRequested NewRequest() => new(
        SubmissionId: Guid.NewGuid(),
        QuestionId: Guid.NewGuid(),
        Language: "python",
        SourceCode: "print(1)",
        TimeLimitMs: 1500,
        MemoryLimitMb: 128,
        TestCases:
        [
            new JudgeTestCase(Guid.NewGuid(), 1, "1", "one"),
            new JudgeTestCase(Guid.NewGuid(), 2, "2", "two")
        ]);

    private static SubmissionJudged NewJudged(JudgeVerdict verdict) => new(
        Guid.NewGuid(),
        verdict,
        [new TestCaseResult(Guid.NewGuid(), 1, Passed: false, ActualOutput: "4", ErrorMessage: null, ExecutionTimeMs: 1234)]);
}
