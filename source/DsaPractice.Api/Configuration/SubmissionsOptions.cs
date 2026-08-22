namespace DsaPractice.Api.Configuration;

internal sealed class SubmissionsOptions
{
    public const string SectionName = "Submissions";

    public required string[] SupportedLanguages { get; init; }
}
