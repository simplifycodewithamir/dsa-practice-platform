namespace DsaPractice.Api.Exceptions;

internal sealed class NotFoundException(string detail, object? extendedDetail = null)
    : ApiException(StatusCodes.Status404NotFound.ToApiErrorTitle(), StatusCodes.Status404NotFound, detail, extendedDetail);
