using DsaPractice.DataAccess.Enums;
using DsaPractice.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DsaPractice.DataAccess.Configurations;

internal sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        // Restrict: a question with submissions against it can't be deleted out from under them.
        builder.HasOne<Question>()
            .WithMany()
            .HasForeignKey(s => s.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.CompileOutput).HasMaxLength(SubmissionTestResult.MaxOutputLength);
        builder.Property(s => s.Verdict).HasConversion<string>().HasMaxLength(30);

        // Verdict is set exactly when the submission is Completed -- never a verdict on a
        // Pending/Running row, never a Completed row without one.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Submissions_Verdict_OnlyWhenCompleted",
            $"(\"Status\" = '{nameof(SubmissionStatus.Completed)}') = (\"Verdict\" IS NOT NULL)"));
    }
}
