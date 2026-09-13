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
        base.OnModelCreating(modelBuilder);
        // One IEntityTypeConfiguration<T> per entity under Configurations/.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DsaPracticeDbContext).Assembly);
    }
}
