namespace DsaPractice.Api.Exceptions;

internal sealed class BadRequestException(string detail, object? extendedDetail = null)
    : ApiException(StatusCodes.Status400BadRequest.ToApiErrorTitle(), StatusCodes.Status400BadRequest, detail, extendedDetail);
