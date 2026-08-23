using DsaPractice.Api.Exceptions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DsaPractice.Api.UnitTests.Exceptions;

public class ApiErrorTitlesTests
{
    [Theory]
    [InlineData(StatusCodes.Status400BadRequest, ApiErrorTitles.BadRequest)]
    [InlineData(StatusCodes.Status404NotFound, ApiErrorTitles.NotFound)]
    [InlineData(StatusCodes.Status405MethodNotAllowed, ApiErrorTitles.MethodNotAllowed)]
    [InlineData(StatusCodes.Status409Conflict, ApiErrorTitles.Conflict)]
    public void ForStatusCode_KnownStatus_ReturnsMatchingApiErrorTitle(int statusCode, string expectedTitle)
    {
        Assert.Equal(expectedTitle, ApiErrorTitles.ForStatusCode(statusCode));
    }

    [Fact]
    public void ForStatusCode_UnmappedStatus_ReturnsUnknown()
    {
        Assert.Equal(ApiErrorTitles.Unknown, ApiErrorTitles.ForStatusCode(StatusCodes.Status418ImATeapot));
    }
}
