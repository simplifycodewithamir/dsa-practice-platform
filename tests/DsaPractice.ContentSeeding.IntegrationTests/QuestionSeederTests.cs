using DsaPractice.ContentSeeding;
using DsaPractice.DataAccess;
using DsaPractice.DataAccess.Entities;
using DsaPractice.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DsaPractice.ContentSeeding.IntegrationTests;

/// <summary>
/// Runs against a real Postgres with the real migrations applied: the seeder writes without going
/// through the Api's validators, so its output has to satisfy the schema's own check constraints.
/// </summary>
[Collection(SeederTestCollection.Name)]
public class QuestionSeederTests(PostgresFixture fixture)
{
    [Fact]
    public async Task SeedAsync_NewQuestion_InsertsQuestionWithTestCases()
    {
        var content = NewContent(fixture.UniqueSlug());

        var result = await SeedAsync(content);

        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Updated);
        var saved = await LoadAsync(content.Slug);
        Assert.Equal(content.Title, saved.Title);
        Assert.Equal(QuestionDifficulty.Easy, saved.Difficulty);
        Assert.Equal(["array"], saved.Tags);
        Assert.Equal(content.Statement, saved.Description);
        Assert.Equal([1, 2], saved.TestCases.OrderBy(tc => tc.Ordinal).Select(tc => tc.Ordinal));
        Assert.Equal([false, true], saved.TestCases.OrderBy(tc => tc.Ordinal).Select(tc => tc.IsHidden));
    }

    [Fact]
    public async Task SeedAsync_RunTwiceWithSameContent_ChangesNothingTheSecondTime()
    {
        var content = NewContent(fixture.UniqueSlug());
        await SeedAsync(content);
        var idsAfterFirstRun = (await LoadAsync(content.Slug)).TestCases.Select(tc => tc.Id).Order().ToList();

        var result = await SeedAsync(content);

        Assert.Equal(0, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Equal(1, result.Unchanged);
        // Re-running must not churn rows: same question id, same test case ids.
        var saved = await LoadAsync(content.Slug);
        Assert.Equal(idsAfterFirstRun, saved.TestCases.Select(tc => tc.Id).Order());
    }

    [Fact]
    public async Task SeedAsync_EditedStatement_UpdatesInPlaceKeepingTheQuestionId()
    {
        var content = NewContent(fixture.UniqueSlug());
        await SeedAsync(content);
        var originalId = (await LoadAsync(content.Slug)).Id;

        var result = await SeedAsync(content with { Statement = "Rewritten statement.", Title = "Renamed" });

        Assert.Equal(1, result.Updated);
        var saved = await LoadAsync(content.Slug);
        Assert.Equal(originalId, saved.Id); // submissions reference this id
        Assert.Equal("Rewritten statement.", saved.Description);
        Assert.Equal("Renamed", saved.Title);
    }

    [Fact]
    public async Task SeedAsync_TestCaseAdded_AppendsWithoutTouchingExistingRows()
    {
        var content = NewContent(fixture.UniqueSlug());
        await SeedAsync(content);
        var firstCaseId = (await LoadAsync(content.Slug)).TestCases.Single(tc => tc.Ordinal == 1).Id;

        var extended = content with
        {
            TestCases = [.. content.TestCases, new TestCaseContent(3, "9", "nine", IsHidden: true)]
        };
        var result = await SeedAsync(extended);

        Assert.Equal(1, result.Updated);
        var saved = await LoadAsync(content.Slug);
        Assert.Equal([1, 2, 3], saved.TestCases.OrderBy(tc => tc.Ordinal).Select(tc => tc.Ordinal));
        Assert.Equal(firstCaseId, saved.TestCases.Single(tc => tc.Ordinal == 1).Id);
    }

    [Fact]
    public async Task SeedAsync_TestCaseRemoved_DeletesTheLeftoverRow()
    {
        var content = NewContent(fixture.UniqueSlug());
        await SeedAsync(content);

        var trimmed = content with { TestCases = [content.TestCases[0]] };
        var result = await SeedAsync(trimmed);

        Assert.Equal(1, result.TestCasesRemoved);
        var saved = await LoadAsync(content.Slug);
        Assert.Equal([1], saved.TestCases.Select(tc => tc.Ordinal));
    }

    [Fact]
    public async Task SeedAsync_TestCaseEdited_UpdatesValuesInPlace()
    {
        var content = NewContent(fixture.UniqueSlug());
        await SeedAsync(content);

        var edited = content with
        {
            TestCases = [content.TestCases[0] with { ExpectedOutput = "corrected" }, content.TestCases[1]]
        };
        await SeedAsync(edited);

        var saved = await LoadAsync(content.Slug);
        Assert.Equal("corrected", saved.TestCases.Single(tc => tc.Ordinal == 1).ExpectedOutput);
    }

    [Fact]
    public async Task SeedAsync_QuestionInDatabaseButNotInContent_IsLeftAlone()
    {
        var orphan = NewContent(fixture.UniqueSlug());
        await SeedAsync(orphan);

        // Seeding a different question must not delete questions submissions may point at.
        await SeedAsync(NewContent(fixture.UniqueSlug()));

        Assert.NotNull(await FindAsync(orphan.Slug));
    }

    [Fact]
    public async Task SeedAsync_ContentViolatingASchemaRule_RollsBackEverything()
    {
        var good = NewContent(fixture.UniqueSlug());
        var bad = NewContent(fixture.UniqueSlug()) with { TimeLimitMs = 0 }; // CK_Questions_TimeLimitMs_Positive

        await Assert.ThrowsAsync<DbUpdateException>(() => SeedAsync([good, bad]));

        // One transaction for the whole run: the valid question must not be left behind.
        Assert.Null(await FindAsync(good.Slug));
    }

    private Task<SeedResult> SeedAsync(QuestionContent content) => SeedAsync([content]);

    private async Task<SeedResult> SeedAsync(IReadOnlyList<QuestionContent> content)
    {
        await using var db = fixture.CreateDbContext();
        var seeder = new QuestionSeeder(db, NullLogger<QuestionSeeder>.Instance);
        return await seeder.SeedAsync(content, TestContext.Current.CancellationToken);
    }

    private async Task<Question> LoadAsync(string slug) =>
        await FindAsync(slug) ?? throw new InvalidOperationException($"Question '{slug}' was not seeded.");

    private async Task<Question?> FindAsync(string slug)
    {
        await using var db = fixture.CreateDbContext();
        return await db.Questions
            .AsNoTracking()
            .Include(q => q.TestCases)
            .SingleOrDefaultAsync(q => q.Slug == slug, TestContext.Current.CancellationToken);
    }

    private static QuestionContent NewContent(string slug) => new(
        Slug: slug,
        Title: "Two Sum",
        Difficulty: QuestionDifficulty.Easy,
        Tags: ["array"],
        TimeLimitMs: 1000,
        MemoryLimitMb: 256,
        Statement: "A statement.",
        TestCases:
        [
            new TestCaseContent(1, "4 9", "0 1", IsHidden: false),
            new TestCaseContent(2, "3 6", "1 2", IsHidden: true)
        ]);
}
