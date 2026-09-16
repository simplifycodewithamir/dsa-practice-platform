using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DsaPractice.DataAccess.Entities;
using DsaPractice.DataAccess.Enums;

namespace DsaPractice.ContentSeeding;

/// <summary>
/// Reads the questions authored under a content root:
/// <code>
/// content/questions/&lt;slug&gt;/question.json        metadata
/// content/questions/&lt;slug&gt;/statement.md         markdown problem statement
/// content/questions/&lt;slug&gt;/starters/&lt;language&gt;.ext  editor skeleton, optional
/// content/questions/&lt;slug&gt;/tests/sample/NN.in   shown to the user
/// content/questions/&lt;slug&gt;/tests/hidden/NN.in   judged against, never returned by the Api
/// </code>
/// Ordinals are assigned by the loader, samples first then hidden, so authors never hand-number
/// test cases across two folders and collide on the unique (QuestionId, Ordinal) index.
/// Anything else in a question folder (solutions/, notes) is ignored.
/// </summary>
public static class ContentLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    private static readonly Regex SlugRegex = new(Question.SlugPattern, RegexOptions.Compiled);

    // A starter file's stem is a language id, which is a bare lowercase token -- no hyphens, unlike a slug.
    private static readonly Regex LanguageRegex = new("^[a-z0-9]+$", RegexOptions.Compiled);

    public static IReadOnlyList<QuestionContent> Load(string contentRoot)
    {
        var questionsRoot = Path.Combine(contentRoot, "questions");
        if (!Directory.Exists(questionsRoot))
        {
            throw new ContentException($"Content root '{contentRoot}' has no 'questions' directory.");
        }

        var problems = new List<string>();
        var questions = new List<QuestionContent>();

        foreach (var directory in Directory.EnumerateDirectories(questionsRoot).OrderBy(d => d, StringComparer.Ordinal))
        {
            var question = LoadQuestion(directory, problems);
            if (question is not null)
            {
                questions.Add(question);
            }
        }

        // Every broken question in one message: a seeder run that fixes one problem only to fail on
        // the next is a slow way to find out the content is wrong.
        if (problems.Count > 0)
        {
            throw new ContentException($"Invalid question content:{Environment.NewLine}  - {string.Join($"{Environment.NewLine}  - ", problems)}");
        }

        return questions;
    }

    private static QuestionContent? LoadQuestion(string directory, List<string> problems)
    {
        var slug = Path.GetFileName(directory);
        var problemsBefore = problems.Count;

        if (!SlugRegex.IsMatch(slug))
        {
            problems.Add($"{slug}: folder name is not a valid slug (lowercase letters, digits and single hyphens, e.g. 'two-sum').");
            return null;
        }

        var metadata = LoadMetadata(directory, slug, problems);
        var statement = LoadStatement(directory, slug, problems);
        var starters = LoadStarters(directory, slug, problems);
        var testCases = LoadTestCases(directory, slug, problems);

        if (problems.Count > problemsBefore || metadata is null || statement is null)
        {
            return null;
        }

        return new QuestionContent(
            slug,
            metadata.Title,
            metadata.ParsedDifficulty,
            metadata.Tags,
            metadata.TimeLimitMs,
            metadata.MemoryLimitMb,
            statement,
            starters,
            testCases);
    }

    private static QuestionMetadata? LoadMetadata(string directory, string slug, List<string> problems)
    {
        var path = Path.Combine(directory, "question.json");
        if (!File.Exists(path))
        {
            problems.Add($"{slug}: question.json is missing.");
            return null;
        }

        QuestionMetadata? metadata;
        try
        {
            metadata = JsonSerializer.Deserialize<QuestionMetadata>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException exception)
        {
            problems.Add($"{slug}: question.json could not be parsed ({exception.Message}).");
            return null;
        }

        if (metadata is null)
        {
            problems.Add($"{slug}: question.json is empty.");
            return null;
        }

        var problemsBefore = problems.Count;

        if (string.IsNullOrWhiteSpace(metadata.Title))
        {
            problems.Add($"{slug}: question.json 'title' is required.");
        }

        if (!Enum.TryParse<QuestionDifficulty>(metadata.Difficulty, ignoreCase: false, out var difficulty))
        {
            problems.Add($"{slug}: question.json 'difficulty' is '{metadata.Difficulty}'; expected one of {string.Join(", ", Enum.GetNames<QuestionDifficulty>())}.");
        }

        if (metadata.TimeLimitMs <= 0)
        {
            problems.Add($"{slug}: question.json 'timeLimitMs' must be greater than zero.");
        }

        if (metadata.MemoryLimitMb <= 0)
        {
            problems.Add($"{slug}: question.json 'memoryLimitMb' must be greater than zero.");
        }

        if (problems.Count > problemsBefore)
        {
            return null;
        }

        metadata.ParsedDifficulty = difficulty;
        return metadata;
    }

    private static string? LoadStatement(string directory, string slug, List<string> problems)
    {
        var path = Path.Combine(directory, "statement.md");
        if (!File.Exists(path))
        {
            problems.Add($"{slug}: statement.md is missing.");
            return null;
        }

        var statement = Normalise(File.ReadAllText(path));
        if (statement.Length == 0)
        {
            problems.Add($"{slug}: statement.md is empty.");
            return null;
        }

        return statement;
    }

    /// <summary>
    /// Reads starters/&lt;language&gt;.&lt;ext&gt;. The file's stem is the language id ("csharp", "python");
    /// the extension only exists so an editor highlights the file while it's being authored, and is
    /// ignored here. Deliberately not checked against Submissions:SupportedLanguages -- that list is
    /// the Api's, and a second copy of it in the seeder would be a second thing to keep in step. A
    /// starter for a language nothing offers is dead weight, not a broken question.
    /// </summary>
    private static Dictionary<string, string> LoadStarters(string directory, string slug, List<string> problems)
    {
        var starters = new Dictionary<string, string>(StringComparer.Ordinal);

        var startersDirectory = Path.Combine(directory, "starters");
        if (!Directory.Exists(startersDirectory))
        {
            return starters;
        }

        foreach (var path in Directory.EnumerateFiles(startersDirectory).OrderBy(p => p, StringComparer.Ordinal))
        {
            var language = Path.GetFileNameWithoutExtension(path);

            if (!LanguageRegex.IsMatch(language))
            {
                problems.Add($"{slug}: starters/{Path.GetFileName(path)} is not named after a language (lowercase letters and digits, e.g. 'csharp.cs').");
                continue;
            }

            // Two files with the same stem ("csharp.cs" and "csharp.txt") would otherwise have the
            // winner decided by enumeration order -- silently, and differently per filesystem.
            if (starters.ContainsKey(language))
            {
                problems.Add($"{slug}: more than one starter is named '{language}' (the extension is ignored, so the stems must be unique).");
                continue;
            }

            var code = NormaliseSource(File.ReadAllText(path));
            if (code.Trim().Length == 0)
            {
                problems.Add($"{slug}: starters/{Path.GetFileName(path)} is empty.");
                continue;
            }

            starters[language] = code;
        }

        return starters;
    }

    private static List<TestCaseContent> LoadTestCases(string directory, string slug, List<string> problems)
    {
        var testCases = new List<TestCaseContent>();
        var ordinal = 1;

        foreach (var (folder, isHidden) in new[] { ("sample", false), ("hidden", true) })
        {
            var testsDirectory = Path.Combine(directory, "tests", folder);
            if (!Directory.Exists(testsDirectory))
            {
                continue;
            }

            foreach (var inputPath in Directory.EnumerateFiles(testsDirectory, "*.in").OrderBy(p => p, StringComparer.Ordinal))
            {
                var expectedPath = Path.ChangeExtension(inputPath, ".out");
                if (!File.Exists(expectedPath))
                {
                    problems.Add($"{slug}: tests/{folder}/{Path.GetFileName(expectedPath)} is missing (every .in needs a matching .out).");
                    continue;
                }

                testCases.Add(new TestCaseContent(
                    ordinal++,
                    Normalise(File.ReadAllText(inputPath)),
                    Normalise(File.ReadAllText(expectedPath)),
                    isHidden));
            }
        }

        if (!testCases.Any(tc => !tc.IsHidden))
        {
            problems.Add($"{slug}: at least one sample test case is required (tests/sample/01.in + 01.out).");
        }

        return testCases;
    }

    /// <summary>
    /// CRLF from a Windows checkout would otherwise reach the sandbox as different stdin than the
    /// same file on Linux, and an editor's trailing newline isn't part of the expected output.
    /// </summary>
    private static string Normalise(string text) => text.ReplaceLineEndings("\n").TrimEnd('\n');

    /// <summary>
    /// Same CRLF fix as <see cref="Normalise"/>, but source code keeps exactly one trailing newline:
    /// it is opened in an editor rather than compared byte for byte, and a file that ends mid-line
    /// leaves the caret somewhere odd.
    /// </summary>
    private static string NormaliseSource(string text) => $"{Normalise(text)}\n";

    private sealed class QuestionMetadata
    {
        public string Title { get; init; } = string.Empty;
        public string Difficulty { get; init; } = string.Empty;
        public string[] Tags { get; init; } = [];
        public int TimeLimitMs { get; init; }
        public int MemoryLimitMb { get; init; }

        [JsonIgnore]
        public QuestionDifficulty ParsedDifficulty { get; set; }
    }
}
