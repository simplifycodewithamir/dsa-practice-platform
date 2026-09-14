using DsaPractice.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DsaPractice.DataAccess.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.Issuer).HasMaxLength(500);
        builder.Property(u => u.Subject).HasMaxLength(200);
        builder.Property(u => u.DisplayName).HasMaxLength(200);
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);

        // One row per person per provider, and the lookup every authenticated request makes.
        builder.HasIndex(u => new { u.Issuer, u.Subject }).IsUnique();
    }
}
