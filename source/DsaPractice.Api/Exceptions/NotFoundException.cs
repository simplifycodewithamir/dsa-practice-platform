namespace DsaPractice.Api.Exceptions;

internal sealed class NotFoundException(string detail, object? extendedDetail = null)
    : ApiException(ApiErrorTitles.NotFound, StatusCodes.Status404NotFound, detail, extendedDetail);
