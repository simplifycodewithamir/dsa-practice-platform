namespace DsaPractice.Api.Exceptions;

/// <summary>
/// Base for expected, meaningful failures. The global exception handler maps these to ProblemDetails.
/// </summary>
internal abstract class ApiException(ErrorTitle errorTitle, int httpStatusCode, string detail) : Exception(detail)
{
    public ErrorTitle ErrorTitle { get; } = errorTitle;
    public int HttpStatusCode { get; } = httpStatusCode;
    public object? ExtendedDetail { get; }

    protected ApiException(ErrorTitle errorCode, int httpStatusCode, string message, object? extendedDetail) 
        : this(errorCode, httpStatusCode, message)
    {
        ExtendedDetail = extendedDetail;
    }
}