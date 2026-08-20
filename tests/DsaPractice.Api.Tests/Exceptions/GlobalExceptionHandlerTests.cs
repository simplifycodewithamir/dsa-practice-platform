using System.Text.Json;
using DsaPractice.Api.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DsaPractice.Api.Tests.Exceptions;

public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_DomainException_MapsToItsStatusCodeAndErrorCode()
    {
        var (handler, context) = CreateSut();
        var exception = new QuestionNotFoundException(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

        var problemDetails = await ReadProblemDetailsAsync(context);
        Assert.Equal(exception.Message, problemDetails.Title);
        Assert.Equal("api.error.notfound", GetErrorCode(problemDetails));
    }

    [Fact]
    public async Task TryHandleAsync_UnhandledException_MapsTo500WithGenericMessageNotExceptionDetails()
    {
        var (handler, context) = CreateSut();
        var exception = new InvalidOperationException("Host=db;Password=super-secret");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);

        var problemDetails = await ReadProblemDetailsAsync(context);
        Assert.Equal("An unexpected error occurred.", problemDetails.Title);
        Assert.Equal("api.error.internal", GetErrorCode(problemDetails));
    }

    private static (GlobalExceptionHandler Handler, DefaultHttpContext Context) CreateSut()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddProblemDetails();
        var provider = services.BuildServiceProvider();

        var handler = new GlobalExceptionHandler(
            provider.GetRequiredService<IProblemDetailsService>(),
            NullLogger<GlobalExceptionHandler>.Instance);

        var context = new DefaultHttpContext
        {
            RequestServices = provider,
            Response = { Body = new MemoryStream() }
        };

        return (handler, context);
    }

    private static async Task<ProblemDetails> ReadProblemDetailsAsync(DefaultHttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body);
        return Assert.IsType<ProblemDetails>(problemDetails);
    }

    private static string? GetErrorCode(ProblemDetails problemDetails) =>
        ((JsonElement)problemDetails.Extensions["errorCode"]!).GetString();
}
