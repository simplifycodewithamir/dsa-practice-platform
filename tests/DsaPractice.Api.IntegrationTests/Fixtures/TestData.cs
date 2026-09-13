using DsaPractice.DataAccess.Enums;
using DsaPractice.DataAccess;
using DsaPractice.DataAccess.Entities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DsaPractice.Api.IntegrationTests.Fixtures;

internal static class TestData
{
    /// <summary>A valid question with a unique slug; tests override only the fields they're about.</summary>
    public static Question NewQuestion(Action<Question>? customize = null)
    {
        var id = Guid.NewGuid();
        var question = new Question
        {
            Id = id,
            Slug = $"sum-of-two-{id:N}",
            Title = $"Sum of Two {id}",
            Description = "Read two integers and print their sum.",
            Difficulty = QuestionDifficulty.Easy,
            Tags = ["math"],
            TimeLimitMs = 1000,
            MemoryLimitMb = 256
        };

        customize?.Invoke(question);
        return question;
    }

    public static TestCase NewTestCase(Guid questionId, int ordinal, string input, string expectedOutput, bool isHidden = false) => new()
    {
        Id = Guid.NewGuid(),
        QuestionId = questionId,
        Ordinal = ordinal,
        Input = input,
        ExpectedOutput = expectedOutput,
        IsHidden = isHidden
    };

    public static async Task<Question> SeedAsync(ApiWebApplicationFactory factory, Question question)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();

        db.Questions.Add(question);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return question;
    }
}
