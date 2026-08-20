namespace DsaPractice.Api.Exceptions;

/// <summary>Base for expected, meaningful failures. The global exception handler maps these to ProblemDetails.</summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message, string errorCode, int httpStatusCode)
        : base(message)
    {
        ErrorCode = errorCode;
        HttpStatusCode = httpStatusCode;
    }

    public string ErrorCode { get; }
    public int HttpStatusCode { get; }
}
