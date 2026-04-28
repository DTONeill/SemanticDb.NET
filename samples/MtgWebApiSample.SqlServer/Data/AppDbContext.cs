using Microsoft.EntityFrameworkCore;
using MtgWebApiSample.Core.Models;
using SemanticDb.EF.Extensions;
using SemanticDb.EF.SqlServer.Extensions;

namespace MtgWebApiSample.SqlServer.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // For subclassing in tests with a different DbContextOptions<T>
    protected AppDbContext(DbContextOptions options) : base(options) { }

    public DbSet<Card> Cards => Set<Card>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplySemanticDbConfiguration();
        ConfigureVectorSearch(modelBuilder);

        modelBuilder.Entity<Card>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(512);
            entity.Property(c => c.ManaCost).HasMaxLength(64);
            entity.Property(c => c.SetCode).HasMaxLength(20);
        });
    }

    protected virtual void ConfigureVectorSearch(ModelBuilder modelBuilder) =>
        modelBuilder.UseSqlServerVectorSearch();
}
