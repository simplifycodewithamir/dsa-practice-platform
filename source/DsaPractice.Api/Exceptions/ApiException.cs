namespace DsaPractice.Api.Exceptions;

/// <summary>
/// Base for expected, meaningful failures. The global exception handler maps these to ProblemDetails.
/// </summary>
internal abstract class ApiException(string title, int httpStatusCode, string detail, object? extendedDetail = null)
    : Exception(detail)
{
    public string Title { get; } = title;
    public int HttpStatusCode { get; } = httpStatusCode;
    public object? ExtendedDetail { get; } = extendedDetail;
}
