using DsaPractice.Api.Exceptions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DsaPractice.Api.UnitTests.Exceptions;

/// <summary>
/// Each exception type is the single decision of "what status does this failure mean". The handler
/// only copies what it finds here, so this is where the HTTP contract is actually fixed.
/// </summary>
public class ApiExceptionTests
{
    [Fact]
    public void NotFoundException_Is404WithTheNotFoundTitle()
    {
        var exception = new NotFoundException("Question 'two-sum' was not found.");

        Assert.Equal(StatusCodes.Status404NotFound, exception.HttpStatusCode);
        Assert.Equal($"{ErrorTitles.ErrorNameSpace}.{ErrorTitles.NotFound}", exception.Title);
    }

    [Fact]
    public void BadRequestException_Is400WithTheBadRequestTitle()
    {
        var exception = new BadRequestException("Language must be one of: csharp, python.");

        Assert.Equal(StatusCodes.Status400BadRequest, exception.HttpStatusCode);
        Assert.Equal($"{ErrorTitles.ErrorNameSpace}.{ErrorTitles.BadRequest}", exception.Title);
    }

    [Fact]
    public void ConflictException_Is409WithTheConflictTitle()
    {
        var exception = new ConflictException("That slug is already taken.");

        Assert.Equal(StatusCodes.Status409Conflict, exception.HttpStatusCode);
        Assert.Equal($"{ErrorTitles.ErrorNameSpace}.{ErrorTitles.Conflict}", exception.Title);
    }

    [Fact]
    public void ApiException_DetailIsTheExceptionMessage()
    {
        // The handler writes Message into ProblemDetails.Detail, so the two must not drift apart.
        var exception = new NotFoundException("Submission 'abc' was not found.");

        Assert.Equal("Submission 'abc' was not found.", exception.Message);
    }

    [Fact]
    public void ApiException_ExtendedDetail_IsCarriedThroughWhenGiven()
    {
        var extendedDetail = new { slug = "two-sum" };

        var exception = new NotFoundException("nope", extendedDetail);

        Assert.Same(extendedDetail, exception.ExtendedDetail);
    }

    [Fact]
    public void ApiException_ExtendedDetail_IsNullWhenOmitted()
    {
        Assert.Null(new NotFoundException("nope").ExtendedDetail);
    }
}
