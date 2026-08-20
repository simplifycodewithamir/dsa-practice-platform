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
        var exception = new DsaPracticeAppException(
            new ErrorTitle("notfound"), 
            StatusCodes.Status404NotFound, 
            "Requested question is not found", 
            new { question = new { id = "123" } });

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

        var problemDetails = await ReadProblemDetailsAsync(context);
        var extendedDetails = GetExtendedDetails(problemDetails);
        Assert.Equal("api.error.notfound", problemDetails.Title);
        Assert.Equal(exception.Message, problemDetails.Detail);
        Assert.Equal("{\"question\":{\"id\":\"123\"}}", extendedDetails);
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
        var extendedDetails = GetExtendedDetails(problemDetails);
        Assert.Equal("Host=db;Password=super-secret", problemDetails.Detail);
        Assert.Equal("api.error.unknownError", problemDetails.Title);
        Assert.Equal("{\"extendedDetail\":\"An unexpected error occurred.\"}", extendedDetails);
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

    private static string? GetExtendedDetails(ProblemDetails problemDetails) => 
        JsonSerializer.Serialize(problemDetails.Extensions["extendedDetail"]);
}
