using Microsoft.AspNetCore.Http;

namespace DsaPractice.Api.Exceptions;

public sealed class QuestionNotFoundException(Guid questionId)
    : DomainException($"Question '{questionId}' was not found.", "api.error.notfound", StatusCodes.Status404NotFound)
{
}
