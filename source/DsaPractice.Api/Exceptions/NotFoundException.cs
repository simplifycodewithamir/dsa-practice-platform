using Microsoft.AspNetCore.Http;

namespace DsaPractice.Api.Exceptions;

internal sealed class NotFoundException(string detail, object? extendedDetail = null)
    : ApiException("api.error.notfound", StatusCodes.Status404NotFound, detail, extendedDetail);
