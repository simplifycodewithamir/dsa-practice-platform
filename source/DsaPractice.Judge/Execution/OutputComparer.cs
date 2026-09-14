namespace DsaPractice.Judge.Execution;

/// <summary>
/// Compares a program's output with the expected output.
///
/// Tolerant of the things that are formatting rather than answers -- trailing spaces on a line,
/// trailing blank lines, CRLF versus LF -- and strict about everything else. A user whose answer
/// is right should not lose to a newline their language's print function added.
/// </summary>
public static class OutputComparer
{
    public static bool Matches(string? actual, string expected) =>
        Normalise(actual) == Normalise(expected);

    public static string Normalise(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var lines = value.ReplaceLineEndings("\n").Split('\n');

        // Trailing whitespace per line, then trailing empty lines.
        var trimmed = lines.Select(line => line.TrimEnd()).ToList();
        while (trimmed.Count > 0 && trimmed[^1].Length == 0)
        {
            trimmed.RemoveAt(trimmed.Count - 1);
        }

        return string.Join('\n', trimmed);
    }
}
