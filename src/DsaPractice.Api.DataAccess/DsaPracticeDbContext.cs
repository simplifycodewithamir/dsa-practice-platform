using DsaPractice.Api.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace DsaPractice.Api.DataAccess;

public sealed class DsaPracticeDbContext(DbContextOptions<DsaPracticeDbContext> options)
    : DbContext(options)
{
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<TestCase> TestCases => Set<TestCase>();
    public DbSet<Submission> Submissions => Set<Submission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // TODO: entity configurations (either fluent here or separate IEntityTypeConfiguration<T> classes)
        base.OnModelCreating(modelBuilder);
    }
}
