using DsaPractice.DataAccess.Enums;

namespace DsaPractice.DataAccess.Entities;

/// <summary>
/// A person, as this application knows them. Created the first time an authenticated request
/// arrives (just-in-time), never through a registration form.
///
/// Identity itself lives with the identity provider (decision D3); what is stored here is the
/// minimum needed to own submissions: which provider vouched for them, that provider's id for
/// them, and what this application lets them do.
/// </summary>
public sealed class User
{
    public Guid Id { get; set; }

    /// <summary>The token's `iss`. Kept so two providers can't collide on the same subject.</summary>
    public required string Issuer { get; set; }

    /// <summary>The token's `sub`: the provider's identifier for this person.</summary>
    public required string Subject { get; set; }

    /// <summary>For showing on their own submissions; never used to identify them.</summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Authorization is this application's decision, not the provider's: roles live here rather
    /// than being read from token claims, so switching provider cannot change who is an admin.
    /// </summary>
    public UserRole Role { get; set; } = UserRole.User;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
