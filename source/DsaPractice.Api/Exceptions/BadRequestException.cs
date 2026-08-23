namespace DsaPractice.Api.Exceptions;

internal sealed class BadRequestException(string detail, object? extendedDetail = null)
    : ApiException(ApiErrorTitles.BadRequest, StatusCodes.Status400BadRequest, detail, extendedDetail);
