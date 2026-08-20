using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace DsaPractice.Api.Exceptions;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "An unhandled exception occurred while processing the request.");

        (string title, int httpStatusCode, string detail, object? extendedDetail) = exception switch
        {
            ApiException apiException => (apiException.Title, apiException.HttpStatusCode, apiException.Message, apiException.ExtendedDetail),
            _ => ("api.error.unknown", StatusCodes.Status500InternalServerError, "An unexpected error occurred.", null)
        };

        httpContext.Response.StatusCode = httpStatusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Title = title,
                Status = httpStatusCode,
                Detail = detail,
                Extensions = { ["extendedDetail"] = extendedDetail }
            }
        });
    }
}
