namespace DsaPractice.Api.Exceptions;

internal sealed class ErrorTitle(string value)
{
    public string Value { get; } = value;

    public override string ToString()
    {
        return "api.error." + Value;
    }
}