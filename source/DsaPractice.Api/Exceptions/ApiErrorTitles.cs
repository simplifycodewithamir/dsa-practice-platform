namespace DsaPractice.Api.Exceptions;

/// <summary>
/// Single source of truth for the "api.error.*" ProblemDetails title convention, shared by
/// thrown <see cref="ApiException"/>s (via <see cref="GlobalExceptionHandler"/>) and
/// framework-generated status codes that never throw (routing misses, wrong verb, etc. --
/// see UseStatusCodePages in Program.cs) so both paths report the same title for the same status.
/// </summary>
internal static class ApiErrorTitles
{
    public const string BadRequest = "api.error.badrequest";
    public const string NotFound = "api.error.notfound";
    public const string Conflict = "api.error.conflict";
    public const string MethodNotAllowed = "api.error.methodnotallowed";
    public const string Unknown = "api.error.unknown";

    public static string ForStatusCode(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => BadRequest,
        StatusCodes.Status404NotFound => NotFound,
        StatusCodes.Status405MethodNotAllowed => MethodNotAllowed,
        StatusCodes.Status409Conflict => Conflict,
        _ => Unknown
    };
}
