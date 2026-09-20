using DsaPractice.Api.Exceptions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DsaPractice.Api.UnitTests.Exceptions;

public class ApiErrorTitlesHelperTests
{
    [Theory]
    [InlineData(StatusCodes.Status400BadRequest, ErrorTitles.BadRequest)]
    // 401 and 403 never come from a thrown ApiException -- the framework produces them and
    // UseStatusCodePages titles them through this same helper, so both paths agree.
    [InlineData(StatusCodes.Status401Unauthorized, ErrorTitles.Unauthorized)]
    [InlineData(StatusCodes.Status403Forbidden, ErrorTitles.Forbidden)]
    [InlineData(StatusCodes.Status404NotFound, ErrorTitles.NotFound)]
    [InlineData(StatusCodes.Status405MethodNotAllowed, ErrorTitles.MethodNotAllowed)]
    [InlineData(StatusCodes.Status409Conflict, ErrorTitles.Conflict)]
    public void ToApiErrorTitle_KnownStatus_ReturnsNamespacedErrorTitle(int statusCode, string expectedErrorName)
    {
        Assert.Equal($"{ErrorTitles.ErrorNameSpace}.{expectedErrorName}", statusCode.ToApiErrorTitle());
    }

    [Theory]
    [InlineData(StatusCodes.Status418ImATeapot)]
    [InlineData(StatusCodes.Status500InternalServerError)]
    [InlineData(StatusCodes.Status503ServiceUnavailable)]
    public void ToApiErrorTitle_UnmappedStatus_ReturnsUnknown(int statusCode)
    {
        // 500 is deliberately unmapped: an unexpected failure gets a generic title, so nothing
        // about what actually broke reaches the caller.
        Assert.Equal($"{ErrorTitles.ErrorNameSpace}.{ErrorTitles.Unknown}", statusCode.ToApiErrorTitle());
    }

    [Fact]
    public void ToApiErrorTitle_EveryTitle_IsLowercaseAndNamespaced()
    {
        // The frontend switches on these strings; casing and prefix are part of the contract.
        int[] statusCodes =
        [
            StatusCodes.Status400BadRequest,
            StatusCodes.Status401Unauthorized,
            StatusCodes.Status403Forbidden,
            StatusCodes.Status404NotFound,
            StatusCodes.Status405MethodNotAllowed,
            StatusCodes.Status409Conflict,
            StatusCodes.Status500InternalServerError
        ];

        Assert.All(statusCodes, statusCode =>
        {
            var title = statusCode.ToApiErrorTitle();
            Assert.StartsWith($"{ErrorTitles.ErrorNameSpace}.", title);
            Assert.Equal(title.ToLowerInvariant(), title);
        });
    }
}
