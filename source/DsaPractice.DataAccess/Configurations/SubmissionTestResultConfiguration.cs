using DsaPractice.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DsaPractice.DataAccess.Configurations;

internal sealed class SubmissionTestResultConfiguration : IEntityTypeConfiguration<SubmissionTestResult>
{
    public void Configure(EntityTypeBuilder<SubmissionTestResult> builder)
    {
        builder.Property(r => r.ActualOutput).HasMaxLength(SubmissionTestResult.MaxOutputLength);
        builder.Property(r => r.ErrorMessage).HasMaxLength(SubmissionTestResult.MaxOutputLength);

        // Results are always read for one submission, in run order.
        builder.HasIndex(r => new { r.SubmissionId, r.Ordinal }).IsUnique();

        builder.HasOne<Submission>()
            .WithMany(s => s.TestResults)
            .HasForeignKey(r => r.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
