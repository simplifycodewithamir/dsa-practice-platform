namespace DsaPractice.Api.Exceptions;

internal sealed class BadRequestException(string detail, object? extendedDetail = null)
    : ApiException("api.error.badrequest", StatusCodes.Status400BadRequest, detail, extendedDetail);
