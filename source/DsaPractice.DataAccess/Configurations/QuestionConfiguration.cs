using DsaPractice.Api.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DsaPractice.Api.DataAccess.Configurations;

internal sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.Property(q => q.Slug).HasMaxLength(Question.SlugMaxLength);
        builder.HasIndex(q => q.Slug).IsUnique();

        builder.Property(q => q.Title).HasMaxLength(200);

        // Stored by name, not ordinal: readable in psql, and safe if members are ever reordered.
        builder.Property(q => q.Difficulty).HasConversion<string>().HasMaxLength(10);

        // List<string> maps to a native Postgres text[] (Npgsql convention) -- no join table needed
        // at this size. Add a GIN index if tag filtering ever moves into SQL.

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Questions_Slug_Format", $"\"Slug\" ~ '{Question.SlugPattern}'");
            t.HasCheckConstraint("CK_Questions_TimeLimitMs_Positive", "\"TimeLimitMs\" > 0");
            t.HasCheckConstraint("CK_Questions_MemoryLimitMb_Positive", "\"MemoryLimitMb\" > 0");
        });
    }
}
