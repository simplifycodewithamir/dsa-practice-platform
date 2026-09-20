using System.Text.Json;
using DsaPractice.Api.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DsaPractice.Api.UnitTests.Exceptions;

public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_ApiException_MapsToItsStatusCodeAndTitle()
    {
        var (handler, context) = CreateSut();
        var exception = new NotFoundException(
            "Requested question is not found",
            new { question = new { id = "123" } });

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

        var problemDetails = await ReadProblemDetailsAsync(context);
        Assert.Equal("api.error.notfound", problemDetails.Title);
        Assert.Equal(exception.Message, problemDetails.Detail);
        Assert.Equal("{\"question\":{\"id\":\"123\"}}", GetExtendedDetails(problemDetails));
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
        Assert.Equal("api.error.unknown", problemDetails.Title);
        Assert.Equal("An unexpected error occurred.", problemDetails.Detail);
        Assert.DoesNotContain("super-secret", problemDetails.Detail);
        Assert.Equal("null", GetExtendedDetails(problemDetails));
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest, "api.error.badrequest")]
    [InlineData(StatusCodes.Status409Conflict, "api.error.conflict")]
    public async Task TryHandleAsync_EveryApiExceptionType_UsesItsOwnStatusAndTitle(int expectedStatus, string expectedTitle)
    {
        var (handler, context) = CreateSut();
        ApiException exception = expectedStatus == StatusCodes.Status400BadRequest
            ? new BadRequestException("bad")
            : new ConflictException("clash");

        await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal(expectedTitle, (await ReadProblemDetailsAsync(context)).Title);
    }

    [Fact]
    public async Task TryHandleAsync_ApiExceptionWithoutExtendedDetail_StillWritesTheKey()
    {
        // The frontend reads problem.extendedDetail unconditionally; the key is always present,
        // with a null value, rather than sometimes missing.
        var (handler, context) = CreateSut();

        await handler.TryHandleAsync(context, new ConflictException("clash"), CancellationToken.None);

        var problemDetails = await ReadProblemDetailsAsync(context);
        Assert.True(problemDetails.Extensions.ContainsKey("extendedDetail"));
        Assert.Equal("null", GetExtendedDetails(problemDetails));
    }

    [Fact]
    public async Task TryHandleAsync_StatusIsSetOnTheResponseAndInTheBody()
    {
        // A ProblemDetails whose status disagrees with the response status is unreadable for a
        // client that trusts either one.
        var (handler, context) = CreateSut();

        await handler.TryHandleAsync(context, new NotFoundException("nope"), CancellationToken.None);

        var problemDetails = await ReadProblemDetailsAsync(context);
        Assert.Equal(context.Response.StatusCode, problemDetails.Status);
    }

    [Fact]
    public async Task TryHandleAsync_UnhandledException_LeaksNeitherTypeNorStackTrace()
    {
        var (handler, context) = CreateSut();

        await handler.TryHandleAsync(context, new NpgsqlishException(), CancellationToken.None);

        var body = await ReadBodyAsync(context);
        Assert.DoesNotContain(nameof(NpgsqlishException), body);
        Assert.DoesNotContain("at DsaPractice", body);
    }

    private sealed class NpgsqlishException() : Exception("relation \"Questions\" does not exist");

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

    private static async Task<string> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await new StreamReader(context.Response.Body).ReadToEndAsync();
    }

    private static string? GetExtendedDetails(ProblemDetails problemDetails) =>
        JsonSerializer.Serialize(problemDetails.Extensions["extendedDetail"]);
}
