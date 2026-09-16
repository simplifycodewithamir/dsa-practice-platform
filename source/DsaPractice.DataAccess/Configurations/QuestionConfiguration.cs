using System.Text.Json;
using DsaPractice.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DsaPractice.DataAccess.Configurations;

internal sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    // Keys are language ids, already lowercase -- no camelCase policy, so what is written is what
    // an author would see in psql.
    private static readonly JsonSerializerOptions StartersJsonOptions = new();

    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.Property(q => q.Slug).HasMaxLength(Question.SlugMaxLength);
        builder.HasIndex(q => q.Slug).IsUnique();

        builder.Property(q => q.Title).HasMaxLength(200);

        // Stored by name, not ordinal: readable in psql, and safe if members are ever reordered.
        builder.Property(q => q.Difficulty).HasConversion<string>().HasMaxLength(10);

        // List<string> maps to a native Postgres text[] (Npgsql convention) -- no join table needed
        // at this size. Add a GIN index if tag filtering ever moves into SQL.

        // Same call as Tags: a small map always read with its question and never queried by language
        // on its own doesn't earn a child table. jsonb rather than hstore (which is what Npgsql picks
        // for Dictionary<string, string> by default) so the column needs no Postgres extension.
        //
        // Serialised here rather than by Npgsql: mapping a Dictionary straight onto jsonb needs
        // EnableDynamicJson(), which is a reflection-based opt-in on the data source, so every host
        // that builds one -- Api, migrator, tests -- would have to remember it or fail at runtime.
        // An explicit converter keeps that decision in this file, where the column is defined.
        //
        // The default matters for the migration, not for inserts: the column is NOT NULL and the
        // table already has rows, so adding it without one would fail on an existing database.
        builder.Property(q => q.Starters)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .HasConversion(
                starters => JsonSerializer.Serialize(starters, StartersJsonOptions),
                json => JsonSerializer.Deserialize<Dictionary<string, string>>(json, StartersJsonOptions) ?? new Dictionary<string, string>(),
                // A dictionary is a mutable reference type: without this, EF compares by reference
                // and a starter edited in place would never be detected as a change.
                new ValueComparer<Dictionary<string, string>>(
                    (left, right) => JsonSerializer.Serialize(left, StartersJsonOptions) == JsonSerializer.Serialize(right, StartersJsonOptions),
                    starters => JsonSerializer.Serialize(starters, StartersJsonOptions).GetHashCode(StringComparison.Ordinal),
                    starters => new Dictionary<string, string>(starters, StringComparer.Ordinal)));

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Questions_Slug_Format", $"\"Slug\" ~ '{Question.SlugPattern}'");
            t.HasCheckConstraint("CK_Questions_TimeLimitMs_Positive", "\"TimeLimitMs\" > 0");
            t.HasCheckConstraint("CK_Questions_MemoryLimitMb_Positive", "\"MemoryLimitMb\" > 0");
        });
    }
}
