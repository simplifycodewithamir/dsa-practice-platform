using DsaPractice.Judge.Execution;
using Xunit;

namespace DsaPractice.Judge.UnitTests.Execution;

/// <summary>
/// The rule being pinned down: formatting a language added is forgiven, content is not. Someone
/// whose answer is right should not lose to the newline their print function appended.
/// </summary>
public class OutputComparerTests
{
    [Theory]
    [InlineData("5", "5")]
    [InlineData("5\n", "5")]                    // trailing newline from print()
    [InlineData("5", "5\n")]
    [InlineData("5   ", "5")]                   // trailing spaces on a line
    [InlineData("5\r\n", "5")]                  // CRLF from a Windows-authored file
    [InlineData("1 2\n3 4\n\n\n", "1 2\n3 4")]  // trailing blank lines
    [InlineData("", "")]
    public void Matches_FormattingDifferencesOnly_IsAMatch(string actual, string expected)
    {
        Assert.True(OutputComparer.Matches(actual, expected));
    }

    [Theory]
    [InlineData("5", "6")]
    [InlineData("5", "")]
    [InlineData(null, "5")]
    [InlineData("1 2", "12")]                   // spacing inside a line is content
    [InlineData("  5", "5")]                    // leading whitespace is content
    [InlineData("1\n2", "2\n1")]                // order is content
    public void Matches_ContentDifferences_IsNotAMatch(string? actual, string expected)
    {
        Assert.False(OutputComparer.Matches(actual, expected));
    }
}
