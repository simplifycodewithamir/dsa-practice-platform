namespace DsaPractice.Api.Exceptions;

/// <summary>
/// Single source of truth for the "api.error.*" ProblemDetails title convention, shared by
/// thrown <see cref="ApiException"/>s (via <see cref="GlobalExceptionHandler"/>) and
/// framework-generated status codes that never throw (routing misses, wrong verb, etc. --
/// see UseStatusCodePages in Program.cs) so both paths report the same title for the same status.
/// </summary>
internal static class ApiErrorTitlesHelper
{
    public static string ToApiErrorTitle(this int statusCode)
    {
        var errorName = statusCode switch
        {
            StatusCodes.Status400BadRequest => ErrorTitles.BadRequest,
            StatusCodes.Status404NotFound => ErrorTitles.NotFound,
            StatusCodes.Status405MethodNotAllowed => ErrorTitles.MethodNotAllowed,
            StatusCodes.Status409Conflict => ErrorTitles.Conflict,
            _ => ErrorTitles.Unknown
        };

        return $"{ErrorTitles.ErrorNameSpace}.{errorName}";
    }
}
