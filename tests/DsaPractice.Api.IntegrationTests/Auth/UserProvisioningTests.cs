using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DsaPractice.Api.Endpoints;
using DsaPractice.Api.IntegrationTests.Fixtures;
using DsaPractice.DataAccess;
using DsaPractice.DataAccess.Entities;
using DsaPractice.DataAccess.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DsaPractice.Api.IntegrationTests.Auth;

/// <summary>
/// Who a submission belongs to, and who may read it back.
///
/// Identity comes from the token; the local user row is created on first sight, because there is
/// no registration form -- the identity provider already did that part (decision D3).
/// </summary>
[Collection(ApiTestCollection.Name)]
public class UserProvisioningTests(ApiWebApplicationFactory factory)
{
    [Fact]
    public async Task Submitting_WithAToken_ProvisionsTheUserOnFirstSight()
    {
        var question = await TestData.SeedAsync(factory, TestData.NewQuestion());
        var subject = $"user-{Guid.NewGuid():N}";

        var submission = await SubmitAsync(question.Id, TestTokens.For(subject, name: "Ada"));

        var user = await FindUserAsync(subject);
        Assert.NotNull(user);
        Assert.Equal("Ada", user.DisplayName);
        Assert.Equal(UserRole.User, user.Role);       // never granted by the token
        Assert.Equal(user.Id, submission.UserId);     // the submission belongs to that row
    }

    [Fact]
    public async Task Submitting_TwiceAsTheSamePerson_ReusesTheSameUser()
    {
        var question = await TestData.SeedAsync(factory, TestData.NewQuestion());
        var token = TestTokens.For($"user-{Guid.NewGuid():N}");

        var first = await SubmitAsync(question.Id, token);
        var second = await SubmitAsync(question.Id, token);

        Assert.Equal(first.UserId, second.UserId);
    }

    [Fact]
    public async Task Submitting_AsDifferentPeople_KeepsThemApart()
    {
        var question = await TestData.SeedAsync(factory, TestData.NewQuestion());

        var mine = await SubmitAsync(question.Id, TestTokens.For($"user-{Guid.NewGuid():N}"));
        var theirs = await SubmitAsync(question.Id, TestTokens.For($"user-{Guid.NewGuid():N}"));

        Assert.NotEqual(mine.UserId, theirs.UserId);
    }

    [Fact]
    public async Task Submitting_CannotChooseWhoTheSubmissionBelongsTo()
    {
        var question = await TestData.SeedAsync(factory, TestData.NewQuestion());
        var token = TestTokens.For($"user-{Guid.NewGuid():N}");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // A userId in the body is simply not part of the contract any more; sending one changes
        // nothing, because identity comes from the token.
        using var response = await client.PostAsJsonAsync(
            "/api/v1/submissions",
            new { questionId = question.Id, userId = Guid.NewGuid(), language = "python", sourceCode = "print(1)" },
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<SubmissionResponse>(TestJson.Options, TestContext.Current.CancellationToken);
        var expected = await FindUserIdForTokenAsync(token);
        Assert.Equal(expected, created!.UserId);
    }

    [Fact]
    public async Task ReadingSomeoneElsesSubmission_Returns404NotForbidden()
    {
        var question = await TestData.SeedAsync(factory, TestData.NewQuestion());
        var mine = await SubmitAsync(question.Id, TestTokens.For($"owner-{Guid.NewGuid():N}"));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.For($"other-{Guid.NewGuid():N}"));
        using var response = await client.GetAsync($"/api/v1/submissions/{mine.Id}", TestContext.Current.CancellationToken);

        // 403 would confirm the id exists, which is itself information about someone else.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReadingYourOwnSubmission_Works()
    {
        var question = await TestData.SeedAsync(factory, TestData.NewQuestion());
        var token = TestTokens.For($"owner-{Guid.NewGuid():N}");
        var mine = await SubmitAsync(question.Id, token);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.GetAsync($"/api/v1/submissions/{mine.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AnAdminCanReadAnyonesSubmission()
    {
        var question = await TestData.SeedAsync(factory, TestData.NewQuestion());
        var mine = await SubmitAsync(question.Id, TestTokens.For($"owner-{Guid.NewGuid():N}"));

        var adminSubject = $"admin-{Guid.NewGuid():N}";
        var adminToken = TestTokens.For(adminSubject);
        await SubmitAsync(question.Id, adminToken);           // provisions them
        await PromoteToAdminAsync(adminSubject);              // the role is ours to grant, not the token's

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        using var response = await client.GetAsync($"/api/v1/submissions/{mine.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Submitting_WithoutAToken_Returns401()
    {
        var question = await TestData.SeedAsync(factory, TestData.NewQuestion());
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/submissions",
            new CreateSubmissionRequest(question.Id, "python", "print(1)"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        // A 401 never throws, so it reaches the client through UseStatusCodePages -- this asserts
        // it still comes back as a ProblemDetails body like every other failure, not as nothing.
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("api.error.unauthorized", problem!.Title);
    }

    [Fact]
    public async Task AnUnreadableToken_Returns401()
    {
        var question = await TestData.SeedAsync(factory, TestData.NewQuestion());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-token");

        using var response = await client.PostAsJsonAsync(
            "/api/v1/submissions",
            new CreateSubmissionRequest(question.Id, "python", "print(1)"),
            TestContext.Current.CancellationToken);

        // Junk is not an identity, and since item 20 it is not anonymous access either.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReadingAQuestion_NeedsNoToken()
    {
        var question = await TestData.SeedAsync(factory, TestData.NewQuestion());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/v1/questions/{question.Slug}", TestContext.Current.CancellationToken);

        // The question bank is the public, indexable half of the product (decision D10): requiring
        // a login to read a problem statement would cost the organic traffic the site runs on.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WithEnforcementOff_SubmittingWithoutAToken_IsAttributedToTheLocalUser()
    {
        // The escape hatch behind Auth:RequireAuthentication: the end-to-end stack and a developer
        // who has not configured an issuer yet still get a submission that belongs to a real user
        // row, so the foreign key holds. Same containers as the parent factory, different config.
        using var anonymousFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Auth:RequireAuthentication"] = "false" })));

        var question = await TestData.SeedAsync(factory, TestData.NewQuestion());
        using var client = anonymousFactory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/submissions",
            new CreateSubmissionRequest(question.Id, "python", "print(1)"),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<SubmissionResponse>(TestJson.Options, TestContext.Current.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == created!.UserId, TestContext.Current.CancellationToken);
        Assert.Equal("local-development", user.Issuer);
        Assert.Equal("anonymous", user.Subject);
    }

    private async Task<SubmissionResponse> SubmitAsync(Guid questionId, string? token)
    {
        using var client = factory.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.PostAsJsonAsync(
            "/api/v1/submissions",
            new CreateSubmissionRequest(questionId, "python", "print(1)"),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SubmissionResponse>(TestJson.Options, TestContext.Current.CancellationToken))!;
    }

    private async Task<User?> FindUserAsync(string subject)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Subject == subject, TestContext.Current.CancellationToken);
    }

    private async Task<Guid> FindUserIdForTokenAsync(string token)
    {
        var subject = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token).Subject;
        return (await FindUserAsync(subject))!.Id;
    }

    private async Task PromoteToAdminAsync(string subject)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();
        var user = await db.Users.SingleAsync(u => u.Subject == subject, TestContext.Current.CancellationToken);
        user.Role = UserRole.Admin;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
