using DsaPractice.ContentSeeding;
using DsaPractice.DataAccess.Enums;
using Xunit;

namespace DsaPractice.ContentSeeding.UnitTests;

/// <summary>
/// The loader's whole job is reading a directory tree, so these tests write real files into a
/// temp folder rather than mocking a filesystem abstraction that exists only for the tests.
/// They stay fast (a few small files) and isolated (each test gets its own folder).
/// </summary>
public sealed class ContentLoaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"dsa-content-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Load_ValidQuestion_MapsMetadataStatementAndTests()
    {
        WriteQuestion("two-sum");

        var questions = ContentLoader.Load(_root);

        var question = Assert.Single(questions);
        Assert.Equal("two-sum", question.Slug);
        Assert.Equal("Two Sum", question.Title);
        Assert.Equal(QuestionDifficulty.Easy, question.Difficulty);
        Assert.Equal(["array", "hash-table"], question.Tags);
        Assert.Equal(1000, question.TimeLimitMs);
        Assert.Equal(256, question.MemoryLimitMb);
        Assert.Equal("A statement.", question.Statement);
    }

    [Fact]
    public void Load_SamplesAndHidden_NumbersSamplesFirstThenHidden()
    {
        WriteQuestion("two-sum", samples: [("1", "one"), ("2", "two")], hidden: [("3", "three")]);

        var question = Assert.Single(ContentLoader.Load(_root));

        Assert.Equal([1, 2, 3], question.TestCases.Select(tc => tc.Ordinal));
        Assert.Equal([false, false, true], question.TestCases.Select(tc => tc.IsHidden));
        Assert.Equal(["1", "2", "3"], question.TestCases.Select(tc => tc.Input));
        Assert.Equal(["one", "two", "three"], question.TestCases.Select(tc => tc.ExpectedOutput));
    }

    [Fact]
    public void Load_MultipleQuestions_ReturnsThemOrderedBySlug()
    {
        WriteQuestion("valid-parentheses");
        WriteQuestion("two-sum");
        WriteQuestion("maximum-subarray");

        var questions = ContentLoader.Load(_root);

        Assert.Equal(["maximum-subarray", "two-sum", "valid-parentheses"], questions.Select(q => q.Slug));
    }

    [Fact]
    public void Load_TestCaseFiles_KeepTrailingNewlineOutOfStoredValue()
    {
        WriteQuestion("two-sum");
        File.WriteAllText(Path.Combine(_root, "questions", "two-sum", "tests", "sample", "01.in"), "4 9\n2 7 11 15\n");
        File.WriteAllText(Path.Combine(_root, "questions", "two-sum", "tests", "sample", "01.out"), "0 1\n");

        var question = Assert.Single(ContentLoader.Load(_root));

        // The trailing newline is an artifact of the file, not part of the expected output.
        Assert.Equal("4 9\n2 7 11 15", question.TestCases[0].Input);
        Assert.Equal("0 1", question.TestCases[0].ExpectedOutput);
    }

    [Fact]
    public void Load_CrlfTestCaseFile_NormalisesToLf()
    {
        WriteQuestion("two-sum");
        File.WriteAllText(Path.Combine(_root, "questions", "two-sum", "tests", "sample", "01.in"), "4 9\r\n2 7 11 15\r\n");

        var question = Assert.Single(ContentLoader.Load(_root));

        // Windows checkouts must not produce different stdin than Linux ones.
        Assert.Equal("4 9\n2 7 11 15", question.TestCases[0].Input);
    }

    [Fact]
    public void Load_MissingRoot_Throws()
    {
        var missing = Path.Combine(_root, "nope");

        var exception = Assert.Throws<ContentException>(() => ContentLoader.Load(missing));

        Assert.Contains(missing, exception.Message);
    }

    [Fact]
    public void Load_SlugNotMatchingPattern_Throws()
    {
        WriteQuestion("Two_Sum");

        var exception = Assert.Throws<ContentException>(() => ContentLoader.Load(_root));

        Assert.Contains("Two_Sum", exception.Message);
        Assert.Contains("lowercase", exception.Message);
    }

    [Fact]
    public void Load_MissingStatement_Throws()
    {
        WriteQuestion("two-sum");
        File.Delete(Path.Combine(_root, "questions", "two-sum", "statement.md"));

        var exception = Assert.Throws<ContentException>(() => ContentLoader.Load(_root));

        Assert.Contains("statement.md", exception.Message);
    }

    [Fact]
    public void Load_NoSampleTestCases_Throws()
    {
        WriteQuestion("two-sum", samples: []);

        var exception = Assert.Throws<ContentException>(() => ContentLoader.Load(_root));

        Assert.Contains("at least one sample", exception.Message);
    }

    [Fact]
    public void Load_InputWithoutMatchingOutput_Throws()
    {
        WriteQuestion("two-sum");
        File.Delete(Path.Combine(_root, "questions", "two-sum", "tests", "sample", "01.out"));

        var exception = Assert.Throws<ContentException>(() => ContentLoader.Load(_root));

        Assert.Contains("01.out", exception.Message);
    }

    [Theory]
    [InlineData("Two Sum", "Trivial", 1000, 256, "Trivial")]
    [InlineData("Two Sum", "Easy", 0, 256, "timeLimitMs")]
    [InlineData("Two Sum", "Easy", 1000, -1, "memoryLimitMb")]
    [InlineData("", "Easy", 1000, 256, "title")]
    public void Load_InvalidMetadata_Throws(string title, string difficulty, int timeLimitMs, int memoryLimitMb, string expectedInMessage)
    {
        WriteQuestion("two-sum");
        File.WriteAllText(Path.Combine(_root, "questions", "two-sum", "question.json"), $$"""
            {
              "title": "{{title}}",
              "difficulty": "{{difficulty}}",
              "tags": ["array"],
              "timeLimitMs": {{timeLimitMs}},
              "memoryLimitMb": {{memoryLimitMb}}
            }
            """);

        var exception = Assert.Throws<ContentException>(() => ContentLoader.Load(_root));

        Assert.Contains(expectedInMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_MalformedJson_ThrowsNamingTheFile()
    {
        WriteQuestion("two-sum");
        File.WriteAllText(Path.Combine(_root, "questions", "two-sum", "question.json"), "{ not json");

        var exception = Assert.Throws<ContentException>(() => ContentLoader.Load(_root));

        Assert.Contains("question.json", exception.Message);
    }

    [Fact]
    public void Load_ReportsEveryProblemAtOnce()
    {
        WriteQuestion("two-sum", samples: []);
        WriteQuestion("bad-question");
        File.Delete(Path.Combine(_root, "questions", "bad-question", "statement.md"));

        var exception = Assert.Throws<ContentException>(() => ContentLoader.Load(_root));

        // One run of the seeder should surface every broken question, not just the first.
        Assert.Contains("two-sum", exception.Message);
        Assert.Contains("bad-question", exception.Message);
    }

    [Fact]
    public void Load_UnknownFilesAndFolders_AreIgnored()
    {
        WriteQuestion("two-sum");
        var questionDirectory = Path.Combine(_root, "questions", "two-sum");
        Directory.CreateDirectory(Path.Combine(questionDirectory, "solutions"));
        File.WriteAllText(Path.Combine(questionDirectory, "solutions", "reference.py"), "print(1)");
        File.WriteAllText(Path.Combine(questionDirectory, "notes.txt"), "scratch");

        var question = Assert.Single(ContentLoader.Load(_root));

        Assert.Single(question.TestCases);
    }

    [Fact]
    public void Load_StartersFolder_KeysByFileStemAndIgnoresTheExtension()
    {
        WriteQuestion("two-sum");
        WriteStarters("two-sum", ("csharp.cs", "// C#"), ("python.py", "# Python"));

        var question = Assert.Single(ContentLoader.Load(_root));

        Assert.Equal(["csharp", "python"], question.Starters.Keys.Order());
        Assert.Equal("// C#\n", question.Starters["csharp"]);
        Assert.Equal("# Python\n", question.Starters["python"]);
    }

    [Fact]
    public void Load_NoStartersFolder_LeavesStartersEmptyRatherThanFailing()
    {
        // A question authored before starters existed is still valid content.
        WriteQuestion("two-sum");

        var question = Assert.Single(ContentLoader.Load(_root));

        Assert.Empty(question.Starters);
    }

    [Fact]
    public void Load_StarterFile_NormalisesCrlfAndEndsWithExactlyOneNewline()
    {
        WriteQuestion("two-sum");
        WriteStarters("two-sum", ("csharp.cs", "class A\r\n{\r\n}\r\n\r\n\r\n"));

        var question = Assert.Single(ContentLoader.Load(_root));

        // CRLF from a Windows checkout would otherwise reach the editor, and a file that ends
        // mid-line leaves the caret somewhere odd.
        Assert.Equal("class A\n{\n}\n", question.Starters["csharp"]);
    }

    [Fact]
    public void Load_StarterNamedAfterSomethingOtherThanALanguage_IsReported()
    {
        WriteQuestion("two-sum");
        WriteStarters("two-sum", ("C-Sharp.cs", "// C#"));

        var exception = Assert.Throws<ContentException>(() => ContentLoader.Load(_root));

        Assert.Contains("starters/C-Sharp.cs is not named after a language", exception.Message);
    }

    [Fact]
    public void Load_TwoStartersWithTheSameStem_IsReportedRatherThanDecidedByEnumerationOrder()
    {
        WriteQuestion("two-sum");
        WriteStarters("two-sum", ("csharp.cs", "// first"), ("csharp.txt", "// second"));

        var exception = Assert.Throws<ContentException>(() => ContentLoader.Load(_root));

        Assert.Contains("more than one starter is named 'csharp'", exception.Message);
    }

    [Fact]
    public void Load_EmptyStarterFile_IsReported()
    {
        WriteQuestion("two-sum");
        WriteStarters("two-sum", ("python.py", "   \n\n"));

        var exception = Assert.Throws<ContentException>(() => ContentLoader.Load(_root));

        Assert.Contains("starters/python.py is empty", exception.Message);
    }

    private void WriteStarters(string slug, params (string FileName, string Code)[] starters)
    {
        var directory = Path.Combine(_root, "questions", slug, "starters");
        Directory.CreateDirectory(directory);

        foreach (var (fileName, code) in starters)
        {
            File.WriteAllText(Path.Combine(directory, fileName), code);
        }
    }

    private void WriteQuestion(
        string slug,
        (string Input, string Output)[]? samples = null,
        (string Input, string Output)[]? hidden = null)
    {
        samples ??= [("in", "out")];
        hidden ??= [];

        var directory = Path.Combine(_root, "questions", slug);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "question.json"), """
            {
              "title": "Two Sum",
              "difficulty": "Easy",
              "tags": ["array", "hash-table"],
              "timeLimitMs": 1000,
              "memoryLimitMb": 256
            }
            """);
        File.WriteAllText(Path.Combine(directory, "statement.md"), "A statement.");

        WriteTests(Path.Combine(directory, "tests", "sample"), samples);
        WriteTests(Path.Combine(directory, "tests", "hidden"), hidden);
    }

    private static void WriteTests(string directory, (string Input, string Output)[] cases)
    {
        Directory.CreateDirectory(directory);
        for (var i = 0; i < cases.Length; i++)
        {
            File.WriteAllText(Path.Combine(directory, $"{i + 1:00}.in"), cases[i].Input);
            File.WriteAllText(Path.Combine(directory, $"{i + 1:00}.out"), cases[i].Output);
        }
    }
}
