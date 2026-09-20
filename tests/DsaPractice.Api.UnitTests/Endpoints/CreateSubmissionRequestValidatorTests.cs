using DsaPractice.Api.Configuration;
using DsaPractice.Api.Endpoints;
using FluentValidation.Results;
using Microsoft.Extensions.Options;
using Xunit;

namespace DsaPractice.Api.UnitTests.Endpoints;

/// <summary>
/// The rules that decide whether a submission is even worth queueing. The supported-language list
/// is configuration ("Submissions:SupportedLanguages"), so these tests configure it rather than
/// assuming what is in appsettings.json.
/// </summary>
public class CreateSubmissionRequestValidatorTests
{
    [Fact]
    public void Validate_WellFormedRequest_IsValid()
    {
        var result = Validate(new CreateSubmissionRequest(Guid.NewGuid(), "python", "print(1)"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyQuestionId_IsInvalid()
    {
        // Guid.Empty is what a missing or unparsed id deserializes to, not a question anyone has.
        var result = Validate(new CreateSubmissionRequest(Guid.Empty, "python", "print(1)"));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateSubmissionRequest.QuestionId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t")]
    public void Validate_BlankSourceCode_IsInvalid(string sourceCode)
    {
        // There is nothing to judge, and a sandbox run is expensive enough not to spend on it.
        var result = Validate(new CreateSubmissionRequest(Guid.NewGuid(), "python", sourceCode));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateSubmissionRequest.SourceCode));
    }

    [Fact]
    public void Validate_UnsupportedLanguage_IsInvalid()
    {
        var result = Validate(new CreateSubmissionRequest(Guid.NewGuid(), "rust", "fn main() {}"));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateSubmissionRequest.Language));
    }

    [Theory]
    [InlineData("Python")]
    [InlineData("PYTHON")]
    [InlineData(" python")]
    public void Validate_LanguageNotSpelledExactlyAsConfigured_IsInvalid(string language)
    {
        // The language string is a key: the Judge looks up its runner by it (Judge:Sandbox:Runners),
        // so "Python" is not "python" and accepting it here would only fail later, in the sandbox.
        var result = Validate(new CreateSubmissionRequest(Guid.NewGuid(), language, "print(1)"));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateSubmissionRequest.Language));
    }

    [Fact]
    public void Validate_UnsupportedLanguage_SaysWhichLanguagesAreSupported()
    {
        var result = Validate(new CreateSubmissionRequest(Guid.NewGuid(), "rust", "fn main() {}"));

        var error = Assert.Single(result.Errors, e => e.PropertyName == nameof(CreateSubmissionRequest.Language));
        Assert.Equal("Language must be one of: csharp, python.", error.ErrorMessage);
    }

    [Fact]
    public void Validate_LanguageListFromConfiguration_IsWhatIsEnforced()
    {
        // Adding a language is a configuration change, not a code change.
        var result = Validate(new CreateSubmissionRequest(Guid.NewGuid(), "rust", "fn main() {}"), "rust");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EverythingWrongAtOnce_ReportsEveryProblem()
    {
        // One round trip should tell the caller everything that is wrong with the request.
        var result = Validate(new CreateSubmissionRequest(Guid.Empty, "rust", ""));

        Assert.Equal(3, result.Errors.Count);
    }

    private static ValidationResult Validate(CreateSubmissionRequest request, params string[] supportedLanguages)
    {
        if (supportedLanguages.Length == 0)
        {
            supportedLanguages = ["csharp", "python"];
        }

        var options = new StubOptionsSnapshot(new SubmissionsOptions { SupportedLanguages = supportedLanguages });

        return new CreateSubmissionRequestValidator(options).Validate(request);
    }

    // Hand-rolled rather than mocked: SubmissionsOptions is internal, and a dynamic proxy over an
    // internal type is not something Castle will build.
    private sealed class StubOptionsSnapshot(SubmissionsOptions options) : IOptionsSnapshot<SubmissionsOptions>
    {
        public SubmissionsOptions Value => options;

        public SubmissionsOptions Get(string? name) => options;
    }
}
