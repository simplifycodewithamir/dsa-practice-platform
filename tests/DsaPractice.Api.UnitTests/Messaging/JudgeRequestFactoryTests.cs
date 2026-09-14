using DsaPractice.Api.Messaging;
using DsaPractice.DataAccess.Entities;
using DsaPractice.DataAccess.Enums;
using Xunit;

namespace DsaPractice.Api.UnitTests.Messaging;

public class JudgeRequestFactoryTests
{
    [Fact]
    public void Create_CarriesEverythingTheJudgeNeedsToRunTheSubmission()
    {
        var question = NewQuestion();
        var submission = NewSubmission(question.Id);

        var message = JudgeRequestFactory.Create(submission, question);

        Assert.Equal(submission.Id, message.SubmissionId);
        Assert.Equal(question.Id, message.QuestionId);
        Assert.Equal("python", message.Language);
        Assert.Equal("print(1)", message.SourceCode);
        Assert.Equal(question.TimeLimitMs, message.TimeLimitMs);
        Assert.Equal(question.MemoryLimitMb, message.MemoryLimitMb);
    }

    [Fact]
    public void Create_IncludesHiddenTestCases()
    {
        var question = NewQuestion();
        var submission = NewSubmission(question.Id);

        var message = JudgeRequestFactory.Create(submission, question);

        // The Judge runs every test case -- hiding them is a read-API concern, not a judging one.
        Assert.Equal(3, message.TestCases.Count);
        Assert.Contains(message.TestCases, tc => tc.ExpectedOutput == "hidden-output");
    }

    [Fact]
    public void Create_OrdersTestCasesByOrdinal()
    {
        var question = NewQuestion();
        // Stored order is not guaranteed; the message must impose the run order.
        question.TestCases = [.. question.TestCases.OrderByDescending(tc => tc.Ordinal)];
        var submission = NewSubmission(question.Id);

        var message = JudgeRequestFactory.Create(submission, question);

        Assert.Equal([1, 2, 3], message.TestCases.Select(tc => tc.Ordinal));
    }

    [Fact]
    public void Create_MapsEachTestCaseIdSoResultsCanBeMatchedBack()
    {
        var question = NewQuestion();
        var submission = NewSubmission(question.Id);

        var message = JudgeRequestFactory.Create(submission, question);

        Assert.Equal(
            question.TestCases.OrderBy(tc => tc.Ordinal).Select(tc => tc.Id),
            message.TestCases.Select(tc => tc.TestCaseId));
    }

    private static Question NewQuestion()
    {
        var question = new Question
        {
            Id = Guid.NewGuid(),
            Slug = "two-sum",
            Title = "Two Sum",
            Description = "statement",
            Difficulty = QuestionDifficulty.Easy,
            TimeLimitMs = 1500,
            MemoryLimitMb = 128
        };

        question.TestCases =
        [
            new TestCase { Id = Guid.NewGuid(), QuestionId = question.Id, Ordinal = 1, Input = "1", ExpectedOutput = "one" },
            new TestCase { Id = Guid.NewGuid(), QuestionId = question.Id, Ordinal = 2, Input = "2", ExpectedOutput = "two" },
            new TestCase { Id = Guid.NewGuid(), QuestionId = question.Id, Ordinal = 3, Input = "3", ExpectedOutput = "hidden-output", IsHidden = true }
        ];

        return question;
    }

    private static Submission NewSubmission(Guid questionId) => new()
    {
        Id = Guid.NewGuid(),
        QuestionId = questionId,
        OwnerUserId = Guid.NewGuid(),
        Language = "python",
        SourceCode = "print(1)",
        SubmittedAtUtc = DateTimeOffset.UnixEpoch
    };
}
