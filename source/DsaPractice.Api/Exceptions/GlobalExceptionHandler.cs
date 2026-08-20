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

        (string errorTitle, int httpStatusCode, string detail, object? extendedDetail) = exception switch
        {
            DsaPracticeAppException appException => (appException.ErrorTitle.ToString(), appException.HttpStatusCode, appException.Message, appException.ExtendedDetail),
            _ => (new ErrorTitle("unknownError").ToString(), StatusCodes.Status500InternalServerError, exception.Message, new {extendedDetail= "An unexpected error occurred." })
        };

        httpContext.Response.StatusCode = httpStatusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Title = errorTitle,
                Status = httpStatusCode,
                Detail = detail,
                Extensions = { ["extendedDetail"] = extendedDetail }
            }
        });
    }
}
