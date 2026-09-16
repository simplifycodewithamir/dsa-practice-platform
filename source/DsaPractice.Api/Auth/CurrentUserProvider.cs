using System.Security.Claims;
using DsaPractice.DataAccess;
using DsaPractice.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DsaPractice.Api.Auth;

internal interface ICurrentUserProvider
{
    /// <summary>
    /// The local user for this request, created on first sight (just-in-time provisioning): there
    /// is no registration form, because the identity provider already did that part.
    /// </summary>
    Task<User> GetOrCreateAsync(CancellationToken cancellationToken);
}

internal sealed class CurrentUserProvider(
    IHttpContextAccessor httpContextAccessor,
    DsaPracticeDbContext db,
    TimeProvider timeProvider,
    IOptions<AuthOptions> options,
    ILogger<CurrentUserProvider> logger) : ICurrentUserProvider
{
    public async Task<User> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        var (issuer, subject, displayName) = Identify(principal);

        var user = await db.Users.FirstOrDefaultAsync(
            u => u.Issuer == issuer && u.Subject == subject, cancellationToken);

        if (user is not null)
        {
            return user;
        }

        user = new User
        {
            Id = Guid.NewGuid(),
            Issuer = issuer,
            Subject = subject,
            DisplayName = displayName,
            CreatedAtUtc = timeProvider.GetUtcNow()
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Provisioned user {UserId} for {Issuer}/{Subject}.", user.Id, issuer, subject);
        return user;
    }

    private (string Issuer, string Subject, string? DisplayName) Identify(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            // Only reachable while Auth:RequireAuthentication is false (see AuthOptions): one shared
            // local user, so submissions still have a real owner and the foreign key still holds.
            return (options.Value.LocalIssuer, "anonymous", "Anonymous");
        }

        var subject = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("An authenticated token carried no subject claim.");

        var issuer = principal.FindFirst("iss")?.Value
            ?? principal.Claims.FirstOrDefault()?.Issuer
            ?? "unknown";

        var displayName = principal.FindFirstValue("name") ?? principal.FindFirstValue(ClaimTypes.Name);

        return (issuer, subject, displayName);
    }
}
