using DsaPractice.DataAccess;
using DsaPractice.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DsaPractice.ContentSeeding;

/// <summary>
/// Upserts authored content into the database, keyed by slug. Idempotent: running it against
/// unchanged content writes nothing, so it can run on every deploy.
/// </summary>
public sealed class QuestionSeeder(DsaPracticeDbContext db, ILogger<QuestionSeeder> logger)
{
    public async Task<SeedResult> SeedAsync(IReadOnlyList<QuestionContent> content, CancellationToken cancellationToken)
    {
        var slugs = content.Select(c => c.Slug).ToList();
        var existing = await db.Questions
            .Include(q => q.TestCases)
            .Where(q => slugs.Contains(q.Slug))
            .ToDictionaryAsync(q => q.Slug, cancellationToken);

        var created = 0;
        var testCasesRemoved = 0;

        foreach (var authored in content)
        {
            if (!existing.TryGetValue(authored.Slug, out var question))
            {
                db.Questions.Add(ToEntity(authored));
                created++;
                continue;
            }

            Apply(authored, question);
            testCasesRemoved += ReconcileTestCases(authored, question);
        }

        // Change tracking decides what actually differs, so re-running on untouched content is a
        // no-op rather than a rewrite of every row.
        var updatedQuestions = CountChangedQuestions();

        // One transaction for the whole run: a schema violation in the last question must not
        // leave the earlier ones half-applied.
        await db.SaveChangesAsync(cancellationToken);

        var result = new SeedResult(
            Created: created,
            Updated: updatedQuestions,
            Unchanged: content.Count - created - updatedQuestions,
            TestCasesRemoved: testCasesRemoved);

        logger.LogInformation(
            "Seeded content: {Created} created, {Updated} updated, {Unchanged} unchanged, {TestCasesRemoved} test cases removed.",
            result.Created, result.Updated, result.Unchanged, result.TestCasesRemoved);

        return result;
    }

    /// <summary>A question counts as updated if it, or any of its test cases, changed.</summary>
    private int CountChangedQuestions()
    {
        var changedQuestionIds = new HashSet<Guid>();

        foreach (var entry in db.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Modified or EntityState.Deleted or EntityState.Added))
            {
                continue;
            }

            switch (entry.Entity)
            {
                case Question question when entry.State is EntityState.Modified:
                    changedQuestionIds.Add(question.Id);
                    break;
                case TestCase testCase:
                    changedQuestionIds.Add(testCase.QuestionId);
                    break;
            }
        }

        // Newly added questions are counted separately as "created".
        var addedQuestionIds = db.ChangeTracker.Entries<Question>()
            .Where(entry => entry.State is EntityState.Added)
            .Select(entry => entry.Entity.Id)
            .ToHashSet();

        return changedQuestionIds.Except(addedQuestionIds).Count();
    }

    private static Question ToEntity(QuestionContent content)
    {
        var question = new Question
        {
            Id = Guid.NewGuid(),
            Slug = content.Slug,
            Title = content.Title,
            Description = content.Statement,
            Difficulty = content.Difficulty,
            Tags = [.. content.Tags],
            TimeLimitMs = content.TimeLimitMs,
            MemoryLimitMb = content.MemoryLimitMb,
            Starters = new Dictionary<string, string>(content.Starters, StringComparer.Ordinal)
        };

        question.TestCases =
        [
            .. content.TestCases.Select(tc => new TestCase
            {
                Id = Guid.NewGuid(),
                QuestionId = question.Id,
                Ordinal = tc.Ordinal,
                Input = tc.Input,
                ExpectedOutput = tc.ExpectedOutput,
                IsHidden = tc.IsHidden
            })
        ];

        return question;
    }

    private static void Apply(QuestionContent content, Question question)
    {
        question.Title = content.Title;
        question.Description = content.Statement;
        question.Difficulty = content.Difficulty;
        question.TimeLimitMs = content.TimeLimitMs;
        question.MemoryLimitMb = content.MemoryLimitMb;

        // Assigning an equal-by-value list would mark the entity modified, so only replace tags
        // when they actually differ.
        if (!question.Tags.SequenceEqual(content.Tags))
        {
            question.Tags = [.. content.Tags];
        }

        // Same reason, and order-insensitive: a jsonb map that round-trips with its keys in a
        // different order is the same starter set, and rewriting it would report every question as
        // updated on every run.
        if (!StartersMatch(question.Starters, content.Starters))
        {
            question.Starters = new Dictionary<string, string>(content.Starters, StringComparer.Ordinal);
        }
    }

    private static bool StartersMatch(Dictionary<string, string> existing, IReadOnlyDictionary<string, string> authored) =>
        existing.Count == authored.Count
        && authored.All(pair => existing.TryGetValue(pair.Key, out var code) && code == pair.Value);

    private int ReconcileTestCases(QuestionContent content, Question question)
    {
        var authoredByOrdinal = content.TestCases.ToDictionary(tc => tc.Ordinal);
        var removed = 0;

        // Matched by ordinal rather than replaced wholesale, so ids survive an edit -- a
        // submission's per-test-case results (item 10) reference them.
        foreach (var existing in question.TestCases.ToList())
        {
            if (!authoredByOrdinal.TryGetValue(existing.Ordinal, out var authored))
            {
                question.TestCases.Remove(existing);
                db.Remove(existing);
                removed++;
                continue;
            }

            existing.Input = authored.Input;
            existing.ExpectedOutput = authored.ExpectedOutput;
            existing.IsHidden = authored.IsHidden;
        }

        var existingOrdinals = question.TestCases.Select(tc => tc.Ordinal).ToHashSet();
        foreach (var authored in content.TestCases.Where(tc => !existingOrdinals.Contains(tc.Ordinal)))
        {
            // Added through the DbSet, and with no Id assigned: a new entity discovered on a
            // tracked parent's navigation is marked Modified rather than Added when its key is
            // already set (the same IsKeySet rule DbContext.Update uses), which would issue an
            // UPDATE against a row that doesn't exist yet.
            var testCase = new TestCase
            {
                QuestionId = question.Id,
                Ordinal = authored.Ordinal,
                Input = authored.Input,
                ExpectedOutput = authored.ExpectedOutput,
                IsHidden = authored.IsHidden
            };

            db.TestCases.Add(testCase);
            question.TestCases.Add(testCase);
        }

        return removed;
    }
}
