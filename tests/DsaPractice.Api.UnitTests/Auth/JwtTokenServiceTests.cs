using System.IdentityModel.Tokens.Jwt;
using DsaPractice.Api.Auth;
using Microsoft.Extensions.Options;
using Xunit;

namespace DsaPractice.Api.UnitTests.Auth;

public class JwtTokenServiceTests
{
    private static readonly JwtOptions TestJwtOptions = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "unit-test-signing-key-not-for-production-use-32chars-min"
    };

    [Fact]
    public void CreateToken_SetsSubRoleAndIssuerAudienceClaims()
    {
        var sut = new JwtTokenService(Options.Create(TestJwtOptions), TimeProvider.System);

        var token = sut.CreateToken("user-1", AppRoles.Admin);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("user-1", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(AppRoles.Admin, jwt.Claims.Single(c => c.Type == "role").Value);
        Assert.Equal(TestJwtOptions.Issuer, jwt.Issuer);
        Assert.Contains(TestJwtOptions.Audience, jwt.Audiences);
    }

    [Fact]
    public void CreateToken_SetsExpiryInTheFuture()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var sut = new JwtTokenService(Options.Create(TestJwtOptions), timeProvider);

        var token = sut.CreateToken("user-1", AppRoles.User);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.True(jwt.ValidTo > timeProvider.GetUtcNow().UtcDateTime);
        Assert.Equal(timeProvider.GetUtcNow().UtcDateTime.AddHours(1), jwt.ValidTo);
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
