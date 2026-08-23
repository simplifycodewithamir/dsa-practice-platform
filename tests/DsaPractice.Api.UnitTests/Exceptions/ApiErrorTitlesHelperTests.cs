using DsaPractice.Api.Exceptions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DsaPractice.Api.UnitTests.Exceptions;

public class ApiErrorTitlesHelperTests
{
    [Theory]
    [InlineData(StatusCodes.Status400BadRequest, ErrorTitles.BadRequest)]
    [InlineData(StatusCodes.Status404NotFound, ErrorTitles.NotFound)]
    [InlineData(StatusCodes.Status405MethodNotAllowed, ErrorTitles.MethodNotAllowed)]
    [InlineData(StatusCodes.Status409Conflict, ErrorTitles.Conflict)]
    public void ToApiErrorTitle_KnownStatus_ReturnsNamespacedErrorTitle(int statusCode, string expectedErrorName)
    {
        Assert.Equal($"{ErrorTitles.ErrorNameSpace}.{expectedErrorName}", statusCode.ToApiErrorTitle());
    }

    [Fact]
    public void ToApiErrorTitle_UnmappedStatus_ReturnsUnknown()
    {
        Assert.Equal($"{ErrorTitles.ErrorNameSpace}.{ErrorTitles.Unknown}", StatusCodes.Status418ImATeapot.ToApiErrorTitle());
    }
}
