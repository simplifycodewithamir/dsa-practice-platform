using DsaPractice.Api.Endpoints;
using DsaPractice.DataAccess.Entities;
using DsaPractice.DataAccess.Enums;
using Xunit;

namespace DsaPractice.Api.UnitTests.Endpoints;

public class QuestionDetailResponseTests
{
    [Fact]
    public void FromEntity_MapsTheQuestionAsAuthored()
    {
        var question = NewQuestion();

        var response = QuestionDetailResponse.FromEntity(question);

        Assert.Equal(question.Id, response.Id);
        Assert.Equal("two-sum", response.Slug);
        Assert.Equal("Two Sum", response.Title);
        Assert.Equal("statement", response.Description);
        Assert.Equal(QuestionDifficulty.Easy, response.Difficulty);
        Assert.Equal(["array", "hash-table"], response.Tags);
        Assert.Equal(1500, response.TimeLimitMs);
        Assert.Equal(128, response.MemoryLimitMb);
    }

    [Fact]
    public void FromEntity_MapsExactlyTheTestCasesThatWereLoaded()
    {
        // The mapping does not filter: the query decides which test cases are loaded, and it never
        // loads hidden ones for this response. Nothing here may add cases back in.
        var question = NewQuestion();
        question.TestCases =
        [
            new TestCase { Id = Guid.NewGuid(), QuestionId = question.Id, Ordinal = 1, Input = "1", ExpectedOutput = "one" },
            new TestCase { Id = Guid.NewGuid(), QuestionId = question.Id, Ordinal = 2, Input = "2", ExpectedOutput = "two" }
        ];

        var response = QuestionDetailResponse.FromEntity(question);

        Assert.Equal([1, 2], response.SampleTestCases.Select(tc => tc.Ordinal));
        Assert.Equal(["one", "two"], response.SampleTestCases.Select(tc => tc.ExpectedOutput));
    }

    [Fact]
    public void FromEntity_KeepsTheOrderTheTestCasesWereLoadedIn()
    {
        var question = NewQuestion();
        question.TestCases =
        [
            new TestCase { Id = Guid.NewGuid(), QuestionId = question.Id, Ordinal = 3, Input = "3", ExpectedOutput = "three" },
            new TestCase { Id = Guid.NewGuid(), QuestionId = question.Id, Ordinal = 1, Input = "1", ExpectedOutput = "one" }
        ];

        var response = QuestionDetailResponse.FromEntity(question);

        // Ordering is the query's job (it orders by ordinal); the mapping must not silently resort
        // and hide a query that stopped doing it.
        Assert.Equal([3, 1], response.SampleTestCases.Select(tc => tc.Ordinal));
    }

    [Fact]
    public void FromEntity_NoTestCasesLoaded_ReturnsAnEmptyListNotNull()
    {
        var response = QuestionDetailResponse.FromEntity(NewQuestion());

        Assert.Empty(response.SampleTestCases);
    }

    [Fact]
    public void FromEntity_CarriesStartersKeyedByLanguage()
    {
        var question = NewQuestion();
        question.Starters = new Dictionary<string, string> { ["csharp"] = "// C#", ["python"] = "# Python" };

        var response = QuestionDetailResponse.FromEntity(question);

        Assert.Equal("// C#", response.Starters["csharp"]);
        Assert.Equal("# Python", response.Starters["python"]);
    }

    [Fact]
    public void FromEntity_QuestionWithNoStarters_ReturnsAnEmptyMapNotNull()
    {
        // The editor asks for a starter by language; an absent map would be a null dereference in
        // the browser rather than "this question has none authored yet".
        var response = QuestionDetailResponse.FromEntity(NewQuestion());

        Assert.Empty(response.Starters);
    }

    private static Question NewQuestion() => new()
    {
        Id = Guid.NewGuid(),
        Slug = "two-sum",
        Title = "Two Sum",
        Description = "statement",
        Difficulty = QuestionDifficulty.Easy,
        Tags = ["array", "hash-table"],
        TimeLimitMs = 1500,
        MemoryLimitMb = 128
    };
}
