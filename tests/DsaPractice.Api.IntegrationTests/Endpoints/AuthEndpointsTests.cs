using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using DsaPractice.Api.Auth;
using DsaPractice.Api.Endpoints;
using DsaPractice.Api.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DsaPractice.Api.IntegrationTests.Endpoints;

[Collection(ApiTestCollection.Name)]
public class AuthEndpointsTests(ApiWebApplicationFactory factory)
{
    [Fact]
    public async Task IssueDevToken_DefaultRole_ReturnsTokenWithUserRoleAndSubClaim()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/dev-token",
            new CreateDevTokenRequest("user-1", null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DevTokenResponse>(TestContext.Current.CancellationToken);
        Assert.Equal("Bearer", body!.TokenType);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken);
        Assert.Equal("user-1", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(AppRoles.User, jwt.Claims.Single(c => c.Type == "role").Value);
    }

    [Fact]
    public async Task IssueDevToken_AdminRole_ReturnsTokenWithAdminRoleClaim()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/dev-token",
            new CreateDevTokenRequest("admin-1", AppRoles.Admin),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DevTokenResponse>(TestContext.Current.CancellationToken);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body!.AccessToken);
        Assert.Equal(AppRoles.Admin, jwt.Claims.Single(c => c.Type == "role").Value);
    }

    [Fact]
    public async Task IssueDevToken_MissingUserId_Returns400()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/dev-token",
            new CreateDevTokenRequest("", null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("api.error.badrequest", problemDetails!.Title);
    }

    [Fact]
    public async Task IssueDevToken_UnknownRole_Returns400()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/dev-token",
            new CreateDevTokenRequest("user-1", "SuperAdmin"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("api.error.badrequest", problemDetails!.Title);
    }
}
