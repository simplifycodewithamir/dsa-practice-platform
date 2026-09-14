using DsaPractice.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DsaPractice.DataAccess.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.Property(m => m.Type).HasMaxLength(200);
        builder.Property(m => m.RoutingKey).HasMaxLength(200);
        builder.Property(m => m.MessageId).HasMaxLength(200);
        builder.Property(m => m.LastError).HasMaxLength(2000);

        // The relay's only query: unprocessed rows that are due, oldest first. Filtered so the
        // index stays small -- processed rows are the overwhelming majority over time and are
        // never looked up through it.
        builder.HasIndex(m => new { m.NextAttemptAtUtc, m.OccurredAtUtc })
            .HasFilter("\"ProcessedAtUtc\" IS NULL")
            .HasDatabaseName("IX_OutboxMessages_Pending");

        // Retention sweeps delete by processed time.
        builder.HasIndex(m => m.ProcessedAtUtc);
    }
}
