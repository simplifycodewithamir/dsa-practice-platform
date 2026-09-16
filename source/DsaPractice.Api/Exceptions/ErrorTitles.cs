namespace DsaPractice.Api.Exceptions;

/// <summary>
/// Error-name constants for the "api.error.*" ProblemDetails title convention. Combined with
/// <see cref="ErrorNameSpace"/> by <see cref="ApiErrorTitlesHelper.ToApiErrorTitle"/>.
/// </summary>
internal static class ErrorTitles
{
    public const string ErrorNameSpace = "api.error";

    public const string BadRequest = "badrequest";
    public const string NotFound = "notfound";
    public const string Conflict = "conflict";
    public const string MethodNotAllowed = "methodnotallowed";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string Unknown = "unknown";
}
