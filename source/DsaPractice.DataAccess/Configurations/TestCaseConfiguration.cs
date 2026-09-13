using DsaPractice.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DsaPractice.DataAccess.Configurations;

internal sealed class TestCaseConfiguration : IEntityTypeConfiguration<TestCase>
{
    public void Configure(EntityTypeBuilder<TestCase> builder)
    {
        // Leading column is QuestionId, so this also serves the FK lookup -- EF drops its
        // convention-created single-column IX_TestCases_QuestionId in favour of it.
        builder.HasIndex(tc => new { tc.QuestionId, tc.Ordinal }).IsUnique();
    }
}
