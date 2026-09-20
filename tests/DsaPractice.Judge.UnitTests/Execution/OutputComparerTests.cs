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

    [Fact]
    public void Matches_InteriorBlankLines_AreContent()
    {
        // Only *trailing* blank lines are formatting; a blank line in the middle is part of the
        // answer's shape.
        Assert.False(OutputComparer.Matches("1\n\n2", "1\n2"));
    }

    [Fact]
    public void Matches_TrailingWhitespaceOnEveryLine_IsForgiven()
    {
        Assert.True(OutputComparer.Matches("1  \t\n2 \n", "1\n2"));
    }

    [Fact]
    public void Matches_MixedLineEndingsWithinOneOutput_StillMatches()
    {
        // A program that prints with \n on some lines and \r\n on others is still correct.
        Assert.True(OutputComparer.Matches("1\r\n2\n3", "1\n2\n3"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\n\n\n")]
    [InlineData("   \n  \n")]
    public void Normalise_NothingButWhitespace_IsTheEmptyString(string? value)
    {
        Assert.Equal(string.Empty, OutputComparer.Normalise(value));
    }

    [Fact]
    public void Normalise_IsIdempotent()
    {
        // Normalising an already-normalised value must not keep changing it, or comparing a stored
        // expected output against a re-normalised one would drift.
        var once = OutputComparer.Normalise("1 \r\n2\n\n");

        Assert.Equal(once, OutputComparer.Normalise(once));
    }

    [Fact]
    public void Normalise_NeverEmitsCarriageReturns()
    {
        Assert.DoesNotContain('\r', OutputComparer.Normalise("a\r\nb\r\n"));
    }

    [Fact]
    public void Matches_ProgramThatPrintedNothing_DoesNotMatchAnExpectedAnswer()
    {
        // A crash before any output must not pass a test whose expected output is non-empty.
        Assert.False(OutputComparer.Matches("", "0"));
        Assert.False(OutputComparer.Matches(null, "0"));
    }

    [Fact]
    public void Matches_BothEmpty_IsAMatch()
    {
        // A question whose expected output is genuinely empty is still answerable.
        Assert.True(OutputComparer.Matches(null, ""));
    }
}
