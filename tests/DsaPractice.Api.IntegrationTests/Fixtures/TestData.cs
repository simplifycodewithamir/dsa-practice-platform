using DsaPractice.DataAccess.Enums;
using DsaPractice.DataAccess;
using DsaPractice.DataAccess.Entities;
using DsaPractice.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>
    /// A user row for a submission to belong to. The issuer matches the tokens TestTokens mints, so
    /// a test can seed an owner and then call the Api as that same person.
    /// </summary>
    public static async Task<User> SeedUserAsync(
        ApiWebApplicationFactory factory,
        string? subject = null,
        UserRole role = UserRole.User)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();

        subject ??= $"seeded-{Guid.NewGuid():N}";

        // Get-or-create, like the Api's own provisioning: a test class that names its owner calls
        // this once per test, and (Issuer, Subject) is unique.
        var existing = await db.Users.FirstOrDefaultAsync(
            u => u.Issuer == TestTokens.Issuer && u.Subject == subject, TestContext.Current.CancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Issuer = TestTokens.Issuer,
            Subject = subject,
            Role = role,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return user;
    }

    public static async Task<Question> SeedAsync(ApiWebApplicationFactory factory, Question question)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();

        db.Questions.Add(question);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return question;
    }
}
