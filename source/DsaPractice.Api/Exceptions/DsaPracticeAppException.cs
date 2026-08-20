namespace DsaPractice.Api.Exceptions;

internal sealed class DsaPracticeAppException : ApiException
{
    public DsaPracticeAppException(ErrorTitle errorCode, int httpStatusCode, string message)
        : base(errorCode, httpStatusCode, message) { }
    public DsaPracticeAppException(ErrorTitle errorCode, int httpStatusCode, string message, object? details)
        : base(errorCode, httpStatusCode, message, details) { }
}