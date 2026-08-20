using Microsoft.AspNetCore.Http;

namespace DsaPractice.Api.Exceptions;

internal sealed class ConflictException(string detail, object? extendedDetail = null)
    : ApiException("api.error.conflict", StatusCodes.Status409Conflict, detail, extendedDetail);
