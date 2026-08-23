namespace DsaPractice.Api.Exceptions;

internal sealed class ForbiddenException(string detail, object? extendedDetail = null)
    : ApiException(StatusCodes.Status403Forbidden.ToApiErrorTitle(), StatusCodes.Status403Forbidden, detail, extendedDetail);
