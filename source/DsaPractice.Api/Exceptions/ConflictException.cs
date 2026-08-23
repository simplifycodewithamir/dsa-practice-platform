namespace DsaPractice.Api.Exceptions;

internal sealed class ConflictException(string detail, object? extendedDetail = null)
    : ApiException(ApiErrorTitles.Conflict, StatusCodes.Status409Conflict, detail, extendedDetail);
