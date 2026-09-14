using DsaPractice.DataAccess.Enums;
using DsaPractice.DataAccess;
using DsaPractice.DataAccess.Entities;
using DsaPractice.Api.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace DsaPractice.Api.IntegrationTests.Persistence;

/// <summary>
/// The database is the last line of defence for these invariants -- the content seeder (item 6)
/// and the Judge result consumer (item 10) both write here, and neither goes through the Api's
/// request validators. Each test asserts the exact constraint that fired, not just "some error".
/// </summary>
[Collection(ApiTestCollection.Name)]
public class DataModelConstraintsTests(ApiWebApplicationFactory factory)
{
    [Fact]
    public async Task SaveQuestion_DuplicateSlug_ViolatesUniqueIndex()
    {
        var existing = await TestData.SeedAsync(factory, TestData.NewQuestion());

        var error = await SaveExpectingFailureAsync(db => db.Questions.Add(TestData.NewQuestion(q => q.Slug = existing.Slug)));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("IX_Questions_Slug", error.ConstraintName);
    }

    [Theory]
    [InlineData("Two-Sum")]   // uppercase
    [InlineData("two_sum")]   // underscore
    [InlineData("-two-sum")]  // leading hyphen
    [InlineData("two--sum")]  // empty segment
    [InlineData("two-sum-")]  // trailing hyphen
    public async Task SaveQuestion_MalformedSlug_ViolatesSlugFormatCheck(string slug)
    {
        var error = await SaveExpectingFailureAsync(db => db.Questions.Add(TestData.NewQuestion(q => q.Slug = slug)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("CK_Questions_Slug_Format", error.ConstraintName);
    }

    [Theory]
    [InlineData(0, 256, "CK_Questions_TimeLimitMs_Positive")]
    [InlineData(1000, 0, "CK_Questions_MemoryLimitMb_Positive")]
    public async Task SaveQuestion_NonPositiveLimit_ViolatesLimitCheck(int timeLimitMs, int memoryLimitMb, string expectedConstraint)
    {
        var error = await SaveExpectingFailureAsync(db => db.Questions.Add(TestData.NewQuestion(q =>
        {
            q.TimeLimitMs = timeLimitMs;
            q.MemoryLimitMb = memoryLimitMb;
        })));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(expectedConstraint, error.ConstraintName);
    }

    [Fact]
    public async Task SaveTestCase_DuplicateOrdinalWithinQuestion_ViolatesUniqueIndex()
    {
        var question = TestData.NewQuestion();
        question.TestCases =
        [
            TestData.NewTestCase(question.Id, ordinal: 1, input: "1 1", expectedOutput: "2"),
            TestData.NewTestCase(question.Id, ordinal: 1, input: "2 2", expectedOutput: "4")
        ];

        var error = await SaveExpectingFailureAsync(db => db.Questions.Add(question));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("IX_TestCases_QuestionId_Ordinal", error.ConstraintName);
    }

    [Fact]
    public async Task SaveTestCase_SameOrdinalOnDifferentQuestions_Succeeds()
    {
        var first = TestData.NewQuestion();
        first.TestCases = [TestData.NewTestCase(first.Id, ordinal: 1, input: "1 1", expectedOutput: "2")];
        var second = TestData.NewQuestion();
        second.TestCases = [TestData.NewTestCase(second.Id, ordinal: 1, input: "2 2", expectedOutput: "4")];

        await TestData.SeedAsync(factory, first);
        await TestData.SeedAsync(factory, second);

        Assert.Equal(1, await CountTestCasesAsync(first.Id));
        Assert.Equal(1, await CountTestCasesAsync(second.Id));
    }

    [Fact]
    public async Task SaveSubmission_UnknownQuestion_ViolatesForeignKey()
    {
        var userId = (await TestData.SeedUserAsync(factory)).Id;

        var error = await SaveExpectingFailureAsync(db => db.Submissions.Add(NewSubmission(Guid.NewGuid(), userId)));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal("FK_Submissions_Questions_QuestionId", error.ConstraintName);
    }

    [Theory]
    [InlineData(SubmissionStatus.Completed, null)]                        // finished but no outcome
    [InlineData(SubmissionStatus.Pending, SubmissionVerdict.Accepted)]    // outcome before it ran
    [InlineData(SubmissionStatus.Running, SubmissionVerdict.WrongAnswer)]
    public async Task SaveSubmission_VerdictNotMatchingStatus_ViolatesVerdictCheck(SubmissionStatus status, SubmissionVerdict? verdict)
    {
        var question = await TestData.SeedAsync(factory, TestData.NewQuestion());
        var userId = (await TestData.SeedUserAsync(factory)).Id;

        var error = await SaveExpectingFailureAsync(db => db.Submissions.Add(NewSubmission(question.Id, userId, status, verdict)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("CK_Submissions_Verdict_OnlyWhenCompleted", error.ConstraintName);
    }

    [Theory]
    [InlineData(SubmissionStatus.Pending, null)]
    [InlineData(SubmissionStatus.Running, null)]
    [InlineData(SubmissionStatus.Completed, SubmissionVerdict.TimeLimitExceeded)]
    public async Task SaveSubmission_VerdictMatchingStatus_Succeeds(SubmissionStatus status, SubmissionVerdict? verdict)
    {
        var question = await TestData.SeedAsync(factory, TestData.NewQuestion());
        var userId = (await TestData.SeedUserAsync(factory)).Id;
        var submission = NewSubmission(question.Id, userId, status, verdict);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();
            db.Submissions.Add(submission);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var readScope = factory.Services.CreateScope();
        var saved = await readScope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>().Submissions
            .AsNoTracking()
            .SingleAsync(s => s.Id == submission.Id, TestContext.Current.CancellationToken);
        Assert.Equal(status, saved.Status);
        Assert.Equal(verdict, saved.Verdict);
    }

    private async Task<PostgresException> SaveExpectingFailureAsync(Action<DsaPracticeDbContext> addEntities)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();
        addEntities(db);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        return Assert.IsType<PostgresException>(exception.InnerException);
    }

    private async Task<int> CountTestCasesAsync(Guid questionId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();
        return await db.TestCases.CountAsync(tc => tc.QuestionId == questionId, TestContext.Current.CancellationToken);
    }

    private static Submission NewSubmission(Guid questionId, Guid userId, SubmissionStatus status = SubmissionStatus.Pending, SubmissionVerdict? verdict = null) => new()
    {
        Id = Guid.NewGuid(),
        QuestionId = questionId,
        OwnerUserId = userId,
        Language = "python",
        SourceCode = "print(1)",
        Status = status,
        Verdict = verdict,
        SubmittedAtUtc = DateTimeOffset.UnixEpoch
    };
}
