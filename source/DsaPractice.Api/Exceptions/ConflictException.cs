namespace DsaPractice.Api.Exceptions;

internal sealed class ConflictException(string detail, object? extendedDetail = null)
    : ApiException(StatusCodes.Status409Conflict.ToApiErrorTitle(), StatusCodes.Status409Conflict, detail, extendedDetail);
